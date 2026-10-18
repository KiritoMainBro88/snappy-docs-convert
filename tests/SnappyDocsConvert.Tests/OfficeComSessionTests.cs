using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Tests for <see cref="OfficeComSession"/> using a fake Office factory (no Office installed). Covers
/// instance reuse across files, per-file document close, quit/dispose at end, and the timeout->kill
/// watchdog that clears the handle so a later file gets a fresh instance.
/// </summary>
public sealed class OfficeComSessionTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(30);

    private static OfficeComSession NewSession(FakeFactory factory)
        => new(
            factory,
            dispatcher: null,
            terminator: factory.Terminator,
            verifier: factory.Verifier,
            disposeQuitGrace: TimeSpan.FromMilliseconds(300));

    [Fact]
    public async Task ConvertFromPdf_ReusesWordInstance_AndCounts()
    {
        var factory = new FakeFactory();
        using var session = NewSession(factory);

        var r1 = await session.ConvertFromPdfAsync(
            OfficeAppKind.Word, "a.pdf", "a.docx", LongTimeout, CancellationToken.None);
        var r2 = await session.ConvertFromPdfAsync(
            OfficeAppKind.Word, "b.pdf", "b.docx", LongTimeout, CancellationToken.None);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.Equal(1, factory.CreateCount(OfficeAppKind.Word));
        Assert.Equal(2, factory.Handles.Single().ExportCount);
    }

    [Fact]
    public async Task Exports_ReuseSingleInstancePerKind_AcrossManyFiles()
    {
        var factory = new FakeFactory();
        using var session = NewSession(factory);

        for (var i = 0; i < 5; i++)
        {
            var r = await session.ExportToPdfAsync(
                OfficeAppKind.PowerPoint, $"in{i}.pptx", $"out{i}.pdf", LongTimeout, CancellationToken.None);
            Assert.True(r.Success);
        }

        Assert.Equal(1, factory.CreateCount(OfficeAppKind.PowerPoint));
        Assert.Equal(5, session.GetReuseCount(OfficeAppKind.PowerPoint));
        var handle = factory.Handles.Single();
        Assert.Equal(5, handle.ExportCount); // each file opened+closed a document
    }

    [Fact]
    public async Task Exports_SeparateInstancesPerKind()
    {
        var factory = new FakeFactory();
        using var session = NewSession(factory);

        await session.ExportToPdfAsync(OfficeAppKind.Word, "a.docx", "a.pdf", LongTimeout, CancellationToken.None);
        await session.ExportToPdfAsync(OfficeAppKind.PowerPoint, "b.pptx", "b.pdf", LongTimeout, CancellationToken.None);
        await session.ExportToPdfAsync(OfficeAppKind.Word, "c.docx", "c.pdf", LongTimeout, CancellationToken.None);

        Assert.Equal(1, factory.CreateCount(OfficeAppKind.Word));
        Assert.Equal(1, factory.CreateCount(OfficeAppKind.PowerPoint));
        Assert.Equal(2, session.GetReuseCount(OfficeAppKind.Word));
    }

    [Fact]
    public async Task Dispose_QuitsAndReleasesEveryInstance()
    {
        var factory = new FakeFactory();
        var session = NewSession(factory);
        await session.ExportToPdfAsync(OfficeAppKind.Word, "a.docx", "a.pdf", LongTimeout, CancellationToken.None);
        await session.ExportToPdfAsync(OfficeAppKind.PowerPoint, "b.pptx", "b.pdf", LongTimeout, CancellationToken.None);

        session.Dispose();

        Assert.All(factory.Handles, h => Assert.True(h.Quit_Called));
        Assert.All(factory.Handles, h => Assert.True(h.Disposed));
    }

    [Fact]
    public async Task Timeout_KillsProcess_ClearsHandle_AndNextFileStartsFreshInstance()
    {
        var factory = new FakeFactory();
        // First handle hangs on Export; second behaves normally.
        factory.HangNextExport = true;
        using var session = NewSession(factory);

        var hung = await session.ExportToPdfAsync(
            OfficeAppKind.PowerPoint, "hang.pptx", "out.pdf", ShortTimeout, CancellationToken.None);
        Assert.True(hung.TimedOut);

        var firstHandle = factory.Handles[0];
        Assert.True(firstHandle.Killed); // watchdog killed the hung process

        // Next file must create a NEW instance (the hung handle was cleared).
        var ok = await session.ExportToPdfAsync(
            OfficeAppKind.PowerPoint, "ok.pptx", "out2.pdf", LongTimeout, CancellationToken.None);
        Assert.True(ok.Success);
        Assert.Equal(2, factory.CreateCount(OfficeAppKind.PowerPoint));
    }

    [Fact]
    public async Task Export_Failure_SurfacesInterpretedMessage()
    {
        var factory = new FakeFactory { ThrowOnExport = new InvalidOperationException("Word could not open the document.") };
        using var session = NewSession(factory);

        var r = await session.ExportToPdfAsync(OfficeAppKind.Word, "x.docx", "x.pdf", LongTimeout, CancellationToken.None);

        Assert.False(r.Success);
        Assert.False(r.TimedOut);
        Assert.Equal("Word could not open the document.", r.ErrorMessage);
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndKillsWhenQuitThrows()
    {
        var factory = new FakeFactory { ThrowOnQuit = true };
        var session = NewSession(factory);
        await session.ExportToPdfAsync(OfficeAppKind.PowerPoint, "b.pptx", "b.pdf", LongTimeout, CancellationToken.None);

        session.Dispose();
        session.Dispose(); // no throw

        var handle = factory.Handles.Single();
        Assert.True(handle.Killed); // Quit threw -> fell back to process kill
    }

    // ---- Fakes -------------------------------------------------------------------------------

    private sealed class FakeFactory : IOfficeAppFactory
    {
        private readonly List<FakeHandle> _handles = new();
        private readonly Dictionary<OfficeAppKind, int> _createCounts = new();
        private bool _hangConsumed;

        public bool HangNextExport { get; set; }
        public Exception? ThrowOnExport { get; set; }
        public bool ThrowOnQuit { get; set; }

        /// <summary>Forces the next created handle to look like a shared/unowned instance (no safe kill).</summary>
        public bool NextHandleUnowned { get; set; }

        /// <summary>Forces identity verification to fail for the next kill attempt (PID-reuse simulation).</summary>
        public bool IdentityStale { get; set; }

        public IReadOnlyList<FakeHandle> Handles => _handles;

        public FakeTerminator Terminator { get; }

        public FakeVerifier Verifier { get; }

        public FakeFactory()
        {
            Terminator = new FakeTerminator(this);
            Verifier = new FakeVerifier(this);
        }

        public int CreateCount(OfficeAppKind kind)
            => _createCounts.TryGetValue(kind, out var c) ? c : 0;

        public IOfficeAppHandle Create(OfficeAppKind kind)
        {
            _createCounts[kind] = CreateCount(kind) + 1;
            var hang = HangNextExport && !_hangConsumed;
            if (hang)
            {
                _hangConsumed = true;
            }

            var unowned = NextHandleUnowned;
            NextHandleUnowned = false;
            var handle = new FakeHandle(kind, _handles.Count + 1000, hang, ThrowOnExport, ThrowOnQuit)
            {
                OwnedExclusively = !unowned,
                // A stale identity still resolves to the fake pid so the terminator can match it;
                // the verifier decides whether the identity is "current" right before the kill.
                Identity = new OfficeProcessIdentity(_handles.Count + 1000, FakeProcessName, DefaultCreationTimeUtc)
            };
            _handles.Add(handle);
            return handle;
        }
    }

    private const string FakeProcessName = "SNAPPYFAKE";
    private static readonly DateTime DefaultCreationTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Fake identity verifier: reports "current" unless IdentityStale was armed, which simulates the
    /// OS reusing the recorded PID for a different process between capture and kill.
    /// </summary>
    private sealed class FakeVerifier : IProcessVerifier
    {
        private readonly FakeFactory _factory;

        public FakeVerifier(FakeFactory factory)
        {
            _factory = factory;
        }

        public bool IsCurrent(OfficeProcessIdentity identity)
            => identity is not null
                && string.Equals(identity.ProcessName, FakeProcessName, StringComparison.OrdinalIgnoreCase)
                && identity.StartTimeUtc == DefaultCreationTimeUtc
                && !_factory.IdentityStale;
    }

    private sealed class FakeTerminator : IProcessTerminator
    {
        private readonly FakeFactory _factory;

        public FakeTerminator(FakeFactory factory)
        {
            _factory = factory;
        }

        public void Kill(int processId)
        {
            foreach (var handle in _factory.Handles)
            {
                if (handle.ProcessId == processId)
                {
                    handle.MarkKilled();
                }
            }
        }
    }

    private sealed class FakeHandle : IOfficeAppHandle
    {
        private readonly bool _hang;
        private readonly Exception? _throwOnExport;
        private readonly bool _throwOnQuit;

        public FakeHandle(OfficeAppKind kind, int pid, bool hang, Exception? throwOnExport, bool throwOnQuit)
        {
            Kind = kind;
            ProcessId = pid;
            _hang = hang;
            _throwOnExport = throwOnExport;
            _throwOnQuit = throwOnQuit;
        }

        public OfficeAppKind Kind { get; }
        public int ProcessId { get; }
        public OfficeProcessIdentity? Identity { get; set; }
        public bool OwnedExclusively { get; set; } = true;
        public int ExportCount { get; private set; }
        public bool Quit_Called { get; private set; }
        public bool Disposed { get; private set; }
        public bool Killed { get; private set; }

        public void Export(string inputPath, string outputPdfPath)
        {
            if (_throwOnExport is not null)
            {
                throw _throwOnExport;
            }

            if (_hang)
            {
                // Simulate a hung Office call so the session's timeout watchdog fires.
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            ExportCount++;
        }

        public void ConvertFromPdf(string inputPdfPath, string outputPath)
        {
            // Reverse conversion mirrors Export for the fake: honour throw/hang config and count it.
            if (_throwOnExport is not null)
            {
                throw _throwOnExport;
            }

            if (_hang)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            ExportCount++;
        }

        public void Quit()
        {
            Quit_Called = true;
            if (_throwOnQuit)
            {
                throw new InvalidOperationException("Quit hung.");
            }
        }

        public void Dispose() => Disposed = true;

        // Test hook: the session kills by PID via Process.GetProcessById which won't find our fake
        // pid; to assert kill intent deterministically we expose MarkKilled through a static shim.
        public void MarkKilled() => Killed = true;
    }
}

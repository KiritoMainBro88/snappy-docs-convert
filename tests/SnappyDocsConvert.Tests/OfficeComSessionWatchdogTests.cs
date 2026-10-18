using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Watchdog regression tests for <see cref="OfficeComSession"/>: a hung Office call must never hang
/// the batch or app shutdown, termination happens only for ownership-verified processes, and unsafe
/// recovery poisons the session so later files fail fast (engine fallback keeps the batch moving).
/// </summary>
public sealed class OfficeComSessionWatchdogTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TinyGrace = TimeSpan.FromMilliseconds(150);

    [Fact]
    public async Task Timeout_OwnedAndVerified_TerminatesProcess_DoesNotPoison_NextFileSucceeds()
    {
        var factory = new WatchdogFactory();
        factory.HangNextExport = true;
        using var session = NewSession(factory);

        var hung = await session.ExportToPdfAsync(
            OfficeAppKind.Word, "hang.docx", "out.pdf", ShortTimeout, CancellationToken.None);
        Assert.True(hung.TimedOut);
        Assert.Contains(hung.Warnings, w => w.Contains("terminated the hung Word process"));

        Assert.False(session.IsPoisoned);
        Assert.True(factory.CreatedHandles[0].Killed);
        Assert.Contains(factory.CreatedHandles[0].ProcessId, factory.Terminator.KilledPids);

        // The handle was cleared: the next file starts a fresh instance and succeeds.
        var ok = await session.ExportToPdfAsync(
            OfficeAppKind.Word, "ok.docx", "out2.pdf", LongTimeout, CancellationToken.None);
        Assert.True(ok.Success);
        Assert.Equal(2, factory.CreateCount(OfficeAppKind.Word));
    }

    [Fact]
    public async Task Timeout_SharedInstance_DoesNotKill_PoisonsSession_AndLaterFilesFailFast()
    {
        var factory = new WatchdogFactory { NextHandleUnowned = true, HangNextExport = true };
        using var session = NewSession(factory);

        var hung = await session.ExportToPdfAsync(
            OfficeAppKind.PowerPoint, "hang.pptx", "out.pdf", ShortTimeout, CancellationToken.None);
        Assert.True(hung.TimedOut);
        Assert.Contains(hung.Warnings, w => w.Contains("left the PowerPoint process untouched"));

        // User safety: a shared/unowned instance is never terminated.
        Assert.False(factory.CreatedHandles[0].Killed);
        Assert.Empty(factory.Terminator.KilledPids);

        // The unsalvageable session is poisoned so later files do not each burn a full timeout.
        Assert.True(session.IsPoisoned);
        var poisoned = await session.ExportToPdfAsync(
            OfficeAppKind.PowerPoint, "next.pptx", "out2.pdf", LongTimeout, CancellationToken.None);
        Assert.False(poisoned.Success);
        Assert.False(poisoned.TimedOut);
        Assert.Contains("session is disabled", poisoned.ErrorMessage);
        Assert.Equal(1, factory.CreateCount(OfficeAppKind.PowerPoint)); // factory never reached again
    }

    [Fact]
    public async Task Timeout_PidReused_DoesNotKill_PoisonsSession()
    {
        var factory = new WatchdogFactory { HangNextExport = true, IdentityStale = true };
        using var session = NewSession(factory);

        var hung = await session.ExportToPdfAsync(
            OfficeAppKind.Word, "hang.docx", "out.pdf", ShortTimeout, CancellationToken.None);
        Assert.True(hung.TimedOut);

        // The recorded pid now belongs to a different process: terminating "the same pid" is unsafe.
        Assert.False(factory.CreatedHandles[0].Killed);
        Assert.Empty(factory.Terminator.KilledPids);
        Assert.True(session.IsPoisoned);
    }

    [Fact]
    public async Task Cancellation_DoesNotKill_AndDoesNotPoison()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var factory = new WatchdogFactory { HangNextExport = true };
        using var session = NewSession(factory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExportToPdfAsync(
            OfficeAppKind.Word, "x.docx", "out.pdf", LongTimeout, cts.Token));

        Assert.False(session.IsPoisoned);
        Assert.False(factory.CreatedHandles.Count > 0 && factory.CreatedHandles[0].Killed);
        Assert.Empty(factory.Terminator.KilledPids);
    }

    [Fact]
    public async Task Export_Failure_DoesNotKill_AndDoesNotPoison()
    {
        var factory = new WatchdogFactory
        {
            ThrowOnExport = new InvalidOperationException("Word could not open the document.")
        };
        using var session = NewSession(factory);

        var result = await session.ExportToPdfAsync(
            OfficeAppKind.Word, "x.docx", "x.pdf", LongTimeout, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(session.IsPoisoned);
        Assert.Empty(factory.Terminator.KilledPids);
    }

    [Fact]
    public void Dispose_WhenStaPumpHung_ReturnsBounded_TerminatesOnlyOwnedHandles()
    {
        var factory = new WatchdogFactory();
        var wordOwned = HandleWithVerifiedIdentity(OfficeAppKind.Word, 6101, unowned: false);
        var powerPointShared = HandleWithVerifiedIdentity(OfficeAppKind.PowerPoint, 6102, unowned: true);
        factory.Terminator.ConsultedHandles = new[] { wordOwned, powerPointShared };

        using var session = new OfficeComSession(
            factory,
            dispatcher: new HungDispatcher(),
            terminator: factory.Terminator,
            verifier: factory.Verifier,
            disposeQuitGrace: TinyGrace);
        InjectHandles(session, wordOwned, powerPointShared);

        var stopwatch = Stopwatch.StartNew();
        session.Dispose();
        stopwatch.Stop();

        // Bounded: returns well below any UI-blocking scale although the pump never drains.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.True(wordOwned.Killed);         // exclusive ownership proven -> identity kill allowed
        Assert.False(powerPointShared.Killed); // shared instance: never touched
        Assert.False(wordOwned.Quit_Called);   // quit path never ran because the pump was hung
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AfterTimeoutRecovery()
    {
        var factory = new WatchdogFactory { HangNextExport = true };
        var session = NewSession(factory);
        var hung = await session.ExportToPdfAsync(
            OfficeAppKind.Word, "hang.docx", "out.pdf", ShortTimeout, CancellationToken.None);
        Assert.True(hung.TimedOut);

        session.Dispose();
        session.Dispose(); // must not throw and must not double-kill
        Assert.Single(factory.Terminator.KilledPids);
    }

    // ---- wiring ------------------------------------------------------------------------------

    private static OfficeComSession NewSession(WatchdogFactory factory)
        => new(
            factory,
            dispatcher: null,
            terminator: factory.Terminator,
            verifier: factory.Verifier,
            disposeQuitGrace: TinyGrace);

    private static WatchdogHandle HandleWithVerifiedIdentity(OfficeAppKind kind, int pid, bool unowned)
        => new(kind, pid, unowned, hang: false, throwOnExport: null)
        {
            Identity = new OfficeProcessIdentity(
                pid, WatchdogFactory.FakeProcessName, WatchdogFactory.FakeStartTimeUtc)
        };

    /// <summary>Dispatcher that never completes queued work (simulates a STA pump stuck in COM).</summary>
    private sealed class HungDispatcher : IStaDispatcher
    {
        public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
        {
            // The queue never drains, so the returned task never completes: the session's dispose
            // path must not rely on it.
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }

        public Task InvokeAsync(Action work, CancellationToken cancellationToken = default)
            => InvokeAsync<object?>(() =>
            {
                work();
                return null;
            }, cancellationToken);

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Reflect-injects handles into the private registry (used for the stuck-pump dispose scenario,
    /// where the normal lazy-create path can never run).
    /// </summary>
    private static void InjectHandles(OfficeComSession session, params IOfficeAppHandle[] handles)
    {
        var registryField = typeof(OfficeComSession).GetField(
            "_handles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("OfficeComSession._handles not found.");
        var registry = (Dictionary<OfficeAppKind, IOfficeAppHandle>)registryField.GetValue(session)!;
        foreach (var handle in handles)
        {
            registry[handle.Kind] = handle;
        }
    }

    // ---- fakes -------------------------------------------------------------------------------

    private sealed class WatchdogFactory : IOfficeAppFactory
    {
        internal const string FakeProcessName = "SNAPPYFAKE";
        internal static readonly DateTime FakeStartTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly List<WatchdogHandle> _created = new();
        private readonly List<WatchdogHandle> _factoryCreated = new();
        private bool _hangConsumed;

        public WatchdogFactory()
        {
            // The terminator resolves kill intents against every handle that exists so far; a live
            // list reference means late-created handles are still visible at kill time.
            _committedHandles = _created;
            Terminator.ConsultedHandles = _committedHandles;
        }

        private readonly List<WatchdogHandle> _committedHandles;

        public bool HangNextExport { get; set; }
        public Exception? ThrowOnExport { get; set; }
        public bool NextHandleUnowned { get; set; }
        public bool IdentityStale { get; set; }

        public WatchdogTerminator Terminator { get; } = new();

        private WatchdogVerifier? _verifier;
        public WatchdogVerifier Verifier => _verifier ??= new WatchdogVerifier(this);

        public IReadOnlyList<WatchdogHandle> CreatedHandles => _created;

        public int CreateCount(OfficeAppKind kind) => _created.Count(h => h.Kind == kind);

        public void Register(WatchdogHandle handle) => _committedHandles.Add(handle);

        public int NextPid()
        {
            var candidate = 5000 + _created.Count;
            while (_created.Any(h => h.ProcessId == candidate))
            {
                candidate++;
            }

            return candidate;
        }

        public IOfficeAppHandle Create(OfficeAppKind kind)
        {
            var unowned = NextHandleUnowned;
            NextHandleUnowned = false;
            var hang = HangNextExport && !_hangConsumed;
            if (hang)
            {
                _hangConsumed = true;
            }

            var handle = new WatchdogHandle(
                kind,
                NextPid(),
                unowned: unowned,
                hang: hang,
                throwOnExport: ThrowOnExport);
            _created.Add(handle);
            return handle;
        }
    }

    private sealed class WatchdogVerifier : IProcessVerifier
    {
        private readonly WatchdogFactory _factory;

        public WatchdogVerifier(WatchdogFactory factory)
        {
            _factory = factory;
        }

        public int Calls { get; private set; }

        public bool IsCurrent(OfficeProcessIdentity identity)
        {
            Calls++;
            return _factory.IdentityStale is false;
        }
    }

    private sealed class WatchdogTerminator : IProcessTerminator
    {
        public List<int> KilledPids { get; } = new();

        public IEnumerable<WatchdogHandle>? ConsultedHandles { get; set; }

        public void Kill(int processId)
        {
            if (processId <= 0)
            {
                return;
            }

            KilledPids.Add(processId);
            foreach (var handle in ConsultedHandles ?? Array.Empty<WatchdogHandle>())
            {
                if (handle.Identity?.ProcessId == processId)
                {
                    handle.MarkKilled();
                }
            }
        }
    }

    private sealed class WatchdogHandle : IOfficeAppHandle
    {
        private readonly bool _hang;
        private readonly Exception? _throwOnExport;

        public WatchdogHandle(
            OfficeAppKind kind,
            int pid,
            bool unowned,
            bool hang,
            Exception? throwOnExport)
        {
            Kind = kind;
            ProcessId = pid;
            Unowned = unowned;
            _hang = hang;
            _throwOnExport = throwOnExport;
            Identity = new OfficeProcessIdentity(pid, WatchdogFactory.FakeProcessName, WatchdogFactory.FakeStartTimeUtc);
        }

        public bool Unowned { get; }

        public OfficeAppKind Kind { get; }
        public int ProcessId { get; }
        public OfficeProcessIdentity? Identity { get; set; }
        public bool OwnedExclusively => Unowned is false;

        public int ExportCount { get; private set; }
        public bool Quit_Called { get; private set; }
        public bool Disposed { get; private set; }
        public bool Killed { get; private set; }

        public void MarkKilled() => Killed = true;

        public void Export(string inputPath, string outputPdfPath)
        {
            if (_throwOnExport is not null)
            {
                throw _throwOnExport;
            }

            if (_hang)
            {
                // Simulate a hung Office COM call so the watchdog fires on the caller side.
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            ExportCount++;
        }

        public void ConvertFromPdf(string inputPdfPath, string outputPath)
        {
            Export(inputPdfPath, outputPath);
        }

        public void Quit()
        {
            Quit_Called = true;
        }

        public void Dispose() => Disposed = true;
    }
}

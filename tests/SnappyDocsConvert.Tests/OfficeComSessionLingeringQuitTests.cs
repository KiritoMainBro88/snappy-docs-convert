using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Regression tests for the dispose-time lingering-process fallback ("PowerPoint quirk"):
/// Application.Quit can complete while the process is still running. For PROVEN-OWNED handles the
/// session then waits a short grace and applies an identity-verified kill; unproven handles are
/// never touched.
/// </summary>
public sealed class OfficeComSessionLingeringQuitTests
{
    private static readonly TimeSpan TinyGrace = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan TinyExitPoll = TimeSpan.FromMilliseconds(80);

    [Fact]
    public void Dispose_QuitCompleted_OwnedVerified_LingeringProcessIsTerminated()
    {
        var factory = new LingeringFactory();
        using var session = NewSession(factory);

        // Identity pid points at this test process: deterministically alive, so the exit poll
        // deterministically times out and the verified kill must fire through the FAKE terminator
        // (no real process is harmed).
        var pid = Environment.ProcessId;
        var handle = new LingeringHandle(OfficeAppKind.PowerPoint, pid)
        {
            Identity = new(
                pid, Process.GetCurrentProcess().ProcessName, DateTime.UtcNow - TimeSpan.FromSeconds(1))
        };
        LingeringInjector.Inject(session, handle);

        session.Dispose();

        Assert.Contains(pid, factory.Terminator.KilledPids);
    }

    [Fact]
    public void Dispose_QuitCompleted_UnownedHandle_NeverKills()
    {
        var factory = new LingeringFactory();
        using var session = NewSession(factory);

        var pid = Environment.ProcessId;
        var handle = new LingeringHandle(OfficeAppKind.PowerPoint, pid, unowned: true)
        {
            Identity = new(
                pid, Process.GetCurrentProcess().ProcessName, DateTime.UtcNow - TimeSpan.FromSeconds(1))
        };
        LingeringInjector.Inject(session, handle);

        session.Dispose();

        Assert.Empty(factory.Terminator.KilledPids);
    }

    private static OfficeComSession NewSession(LingeringFactory factory)
        => new(
            factory,
            dispatcher: null,
            terminator: factory.Terminator,
            verifier: factory.Verifier,
            disposeQuitGrace: TinyGrace,
            quitExitPollGrace: TinyExitPoll);

    private sealed class LingeringFactory : IOfficeAppFactory
    {
        public LingeringTerminator Terminator { get; } = new();
        public AlwaysCurrentVerifier Verifier => _verifier ??= new();

        private AlwaysCurrentVerifier? _verifier;

        public IOfficeAppHandle Create(OfficeAppKind kind)
            => throw new InvalidOperationException("This test injects handles directly.");
    }

    private sealed class AlwaysCurrentVerifier : IProcessVerifier
    {
        public bool IsCurrent(OfficeProcessIdentity identity) => true;
    }

    private sealed class LingeringTerminator : IProcessTerminator
    {
        public List<int> KilledPids { get; } = new();

        public void Kill(int processId) => KilledPids.Add(processId);
    }

    private sealed class LingeringHandle : IOfficeAppHandle
    {
        public LingeringHandle(OfficeAppKind kind, int pid, bool unowned = false)
        {
            Kind = kind;
            ProcessId = pid;
            OwnedExclusively = !unowned;
        }

        public OfficeAppKind Kind { get; }
        public int ProcessId { get; }
        public bool OwnedExclusively { get; }
        public OfficeProcessIdentity? Identity { get; init; }

        public bool Killed { get; private set; }

        public void Export(string inputPath, string outputPdfPath) => throw new NotSupportedException();
        public void ConvertFromPdf(string inputPdfPath, string outputPath) => throw new NotSupportedException();
        public void Quit()
        {
            // Fake Quit: completes cleanly (like the observed PowerPoint behavior) without ending
            // the process.
        }

        public void Dispose() => Quit();

        public void MarkKilled() => Killed = true;
    }

    internal static class LingeringInjector
    {
        public static void Inject(OfficeComSession session, IOfficeAppHandle handle)
        {
            var registryField = typeof(OfficeComSession).GetField(
                "_handles",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? throw new InvalidOperationException("OfficeComSession._handles not found.");
            var registry = (Dictionary<OfficeAppKind, IOfficeAppHandle>)registryField.GetValue(session)!;
            registry[handle.Kind] = handle;
        }
    }
}

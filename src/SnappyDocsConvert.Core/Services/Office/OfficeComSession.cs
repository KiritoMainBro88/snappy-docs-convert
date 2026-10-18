using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SysProcess = System.Diagnostics.Process;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Keeps one Office Application per app-kind alive across a batch. Each export reuses the existing/// Application (lazy created on first use); only the document/presentation is opened and closed per
/// file. On a per-file timeout the watchdog terminates ONLY a process whose exclusive ownership and
/// live identity this app can prove (PID-reuse safe); anything else is left untouched and the whole
/// session is poisoned: later files fail fast with a controlled error instead of hanging, and the
/// batch can proceed through engine fallback. Disposal quits/releases handles with a bounded grace
/// so a stuck STA dispatcher can never hang app shutdown.
///
/// All COM-touching work runs on a single <see cref="IStaDispatcher"/> thread to satisfy STA affinity.
/// Office is abstracted via <see cref="IOfficeAppFactory"/>/<see cref="IOfficeAppHandle"/> so the
/// reuse/timeout/dispose logic is unit-testable with no Office installed.
/// </summary>
public sealed class OfficeComSession : IOfficeComSession
{
    /// <summary>Grace for the STA-thread Quit during dispose before falling back to identity-verified kills.</summary>
    private static readonly TimeSpan DefaultDisposeQuitGrace = TimeSpan.FromSeconds(5);

    private readonly IStaDispatcher _dispatcher;
    private readonly IOfficeAppFactory _factory;
    private readonly IProcessTerminator _terminator;
    private readonly IProcessVerifier _verifier;
    private readonly TimeSpan _disposeQuitGrace;
    private readonly bool _ownsDispatcher;

    private readonly Dictionary<OfficeAppKind, IOfficeAppHandle> _handles = new();
    private readonly Dictionary<OfficeAppKind, int> _reuseCounts = new();
    private readonly object _gate = new();
    private bool _disposed;

    private volatile bool _poisoned;
    private string _poisonReason = string.Empty;

    public OfficeComSession(
        IOfficeAppFactory factory,
        IStaDispatcher? dispatcher = null,
        IProcessTerminator? terminator = null,
        IProcessVerifier? verifier = null,
        TimeSpan? disposeQuitGrace = null,
        TimeSpan? quitExitPollGrace = null)
    {
        _factory = factory;
        _ownsDispatcher = dispatcher is null;
        _dispatcher = dispatcher ?? new StaDispatcher();
        _terminator = terminator ?? new ProcessTerminator();
        _verifier = verifier ?? new WindowsProcessVerifier();
        _disposeQuitGrace = disposeQuitGrace ?? DefaultDisposeQuitGrace;
        if (quitExitPollGrace.HasValue)
        {
            QuitExitPollGrace = quitExitPollGrace.Value;
        }
    }

    /// <summary>True after a hung call could not be recovered safely; all later work fails fast.</summary>
    public bool IsPoisoned => _poisoned;

    public int GetReuseCount(OfficeAppKind appKind)
    {
        lock (_gate)
        {
            return _reuseCounts.TryGetValue(appKind, out var count) ? count : 0;
        }
    }

    public Task<OfficeComExportResult> ExportToPdfAsync(
        OfficeAppKind appKind,
        string inputPath,
        string outputPdfPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => RunOnHandleAsync(
            appKind,
            handle => handle.Export(inputPath, outputPdfPath),
            timeout,
            cancellationToken);

    public Task<OfficeComExportResult> ConvertFromPdfAsync(
        OfficeAppKind appKind,
        string inputPdfPath,
        string outputPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => RunOnHandleAsync(
            appKind,
            handle => handle.ConvertFromPdf(inputPdfPath, outputPath),
            timeout,
            cancellationToken);

    private async Task<OfficeComExportResult> RunOnHandleAsync(
        OfficeAppKind appKind,
        Action<IOfficeAppHandle> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stopwatch = Stopwatch.StartNew();

        if (_poisoned)
        {
            // Controlled failure (not a timeout): the engine maps this to a recoverable status so the
            // batch pipeline can immediately try the eligible fallback engine for every later file.
            return OfficeComExportResult.Failed(
                $"The Office {appKind} session is disabled for this batch after a hang that could not be recovered safely ({_poisonReason}).",
                stopwatch.Elapsed);
        }

        var work = _dispatcher.InvokeAsync(() =>
        {
            var handle = GetOrCreateHandle(appKind);
            action(handle);
            return true;
        }, cancellationToken);

        try
        {
            // Watchdog: the caller thread stops waiting after `timeout` while the STA pump may stay
            // blocked inside the hung COM call. Recovery is therefore never dispatcher-dependent.
            await work.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            lock (_gate)
            {
                _reuseCounts[appKind] = (_reuseCounts.TryGetValue(appKind, out var c) ? c : 0) + 1;
            }

            return OfficeComExportResult.Succeeded(stopwatch.Elapsed);
        }
        catch (TimeoutException)
        {
            // Take the handle out under the gate so the next file creates a fresh instance instead of
            // reusing the possibly-poisoned application object.
            var handle = TakeHandle(appKind);
            var terminated = false;
            if (handle is not null)
            {
                terminated = TryTerminate(handle);
                ObserveFault(work);
            }

            stopwatch.Stop();
            if (!terminated)
            {
                // Fail closed: without proof of exclusive ownership the Office process stays untouched
                // (it could be the user's PowerPoint and contain unsaved work). The pump is probably
                // still blocked, so poison the session to keep later files from waiting on dead code.
                var reason = handle is null
                    ? "no application instance existed to recover."
                    : "the process could not be proven to belong to this app, so it was not terminated.";
                Poison($"a hung {appKind} call exceeded the timeout and {reason}");
            }

            var warning = terminated
                ? $"Watchdog terminated the hung {appKind} process (pid {handle!.ProcessId}). The next file starts a fresh instance."
                : $"Watchdog left the {appKind} process untouched (ownership unproven or no instance); this Office session is disabled for the rest of the batch.";
            return OfficeComExportResult.Timeout(stopwatch.Elapsed, new[] { warning });
        }
        catch (OperationCanceledException)
        {
            ObserveFault(work);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var message = OfficeComExceptionInterpreter.ResolveFailureMessage(ex);
            return OfficeComExceptionInterpreter.TryDescribeDiagnostics(ex, out var diagnostic) && diagnostic is not null
                ? new OfficeComExportResult(false, false, stopwatch.Elapsed, message, new[] { diagnostic })
                : OfficeComExportResult.Failed(message, stopwatch.Elapsed);
        }
    }

    private IOfficeAppHandle? TakeHandle(OfficeAppKind appKind)
    {
        lock (_gate)
        {
            if (!_handles.TryGetValue(appKind, out var handle))
            {
                return null;
            }

            _handles.Remove(appKind);
            return handle;
        }
    }

    /// <summary>
    /// Terminates the handle's process only when the handle reports exclusive ownership AND its
    /// recorded identity still matches the live OS process (guards PID reuse). Otherwise no-op.
    /// Thread-safe: called from the caller thread (watchdog) or during dispose while the STA pump may
    /// be blocked.
    /// </summary>
    private bool TryTerminate(IOfficeAppHandle handle)
    {
        var identity = handle.Identity;
        if (!handle.OwnedExclusively || identity is null)
        {
            return false;
        }

        if (!_verifier.IsCurrent(identity))
        {
            // Identity mismatch = PID reuse or process died. Never kill a mismatching process.
            return false;
        }

        _terminator.Kill(identity.ProcessId);
        return true;
    }

    private void Poison(string reason)
    {
        lock (_gate)
        {
            if (!_poisoned)
            {
                _poisoned = true;
                _poisonReason = reason;
            }
        }
    }

    /// <summary>Grace window given to a completed Quit before the lingering-process fallback kills.</summary>
    internal TimeSpan QuitExitPollGrace { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Terminates a handle's process only when Quit completed but the process defied it: exclusive
    /// ownership must be proven AND the live OS identity must still match (guards PID reuse). Used
    /// for the observed PowerPoint behavior where Application.Quit returns without ending the
    /// process; the verified kill applies the same safety gates as the watchdog.
    /// </summary>
    private bool TryTerminateWhenLingering(IOfficeAppHandle handle)
    {
        var identity = handle.Identity;
        if (!handle.OwnedExclusively || identity is null || !_verifier.IsCurrent(identity))
        {
            return false;
        }

        try
        {
            using var process = SysProcess.GetProcessById(identity.ProcessId);
            if (process.WaitForExit((int)QuitExitPollGrace.TotalMilliseconds))
            {
                return false; // Quit won after all; nothing to do.
            }
        }
        catch (ArgumentException)
        {
            return false; // pid already gone.
        }
        catch (InvalidOperationException)
        {
            // Unreadable process state: fall through to the identity-verified kill below.
        }

        _terminator.Kill(identity.ProcessId);
        return true;
    }

    private IOfficeAppHandle GetOrCreateHandle(OfficeAppKind appKind)
    {
        // Runs on the STA thread. Reuse the existing Application or create one lazily.
        lock (_gate)
        {
            if (_handles.TryGetValue(appKind, out var existing))
            {
                return existing;
            }
        }

        // Create outside the gate: factory work can take seconds and it never races the watchdog
        // path (the STA thread serializes factory calls with export calls by construction).
        var created = _factory.Create(appKind);
        lock (_gate)
        {
            _handles[appKind] = created;
        }

        return created;
    }

    private static void ObserveFault(Task task)
        => _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    /// <summary>
    /// Queue the Quit+Release loop onto the STA thread. A broken/disposed dispatcher must not throw:
    /// a never-completing task keeps the bounded-wait path and, specifically, does NOT count as a
    /// completed quit, so the identity-verified kill fallback still runs.
    /// </summary>
    private Task InvokeQuitSafely(IOfficeAppHandle[] handles, List<IOfficeAppHandle> quitThrew)
    {
        try
        {
            return _dispatcher.InvokeAsync(() =>
            {
                foreach (var handle in handles)
                {
                    try
                    {
                        handle.Quit();
                        handle.Dispose();
                    }
                    catch
                    {
                        // Quit hung or threw: remember it so the identity-verified kill fallback runs
                        // after the wait. Do not kill here — the caller owns that decision.
                        lock (_gate)
                        {
                            quitThrew.Add(handle);
                        }
                    }
                }
            });
        }
        catch
        {
            // Dispatcher already disposed or broken: quit never ran; fall back to kills.
            return Task.Delay(Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Quit + release every Application on the STA thread that created them, with a bounded grace:
        // when the pump is stuck in a hung COM call, this cannot be relied upon.
        IOfficeAppHandle[] handles;
        lock (_gate)
        {
            handles = _handles.Values.ToArray();
            _handles.Clear();
        }

        if (handles.Length > 0)
        {
            var quitThrew = new List<IOfficeAppHandle>();
            var quitTask = InvokeQuitSafely(handles, quitThrew);

            var quitCompleted = false;
            try
            {
                quitTask.Wait(_disposeQuitGrace);
                quitCompleted = quitTask.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted;
            }
            catch
            {
                // AggregateException from Quit failures inside the task: still counts as completed
                // work; the per-handle try/catch above swallowed the actual failures.
                quitCompleted = true;
            }

            bool quitThrewAnything;
            lock (_gate)
            {
                quitThrewAnything = quitThrew.Count > 0;
            }

            if (!quitCompleted || quitThrewAnything)
            {
                // The pump never reached the loop (stuck item), Quit blocked, or Quit threw for some
                // handle. Terminate ONLY the processes whose exclusive ownership from the live
                // identity can be proven; the unowned/shared instance is left alone for user safety.
                // Killing an owned Word/Excel process also unblocks the pump's hung COM call.
                var targets = quitThrewAnything ? (IEnumerable<IOfficeAppHandle>)quitThrew : handles;
                foreach (var handle in targets)
                {
                    TryTerminate(handle);
                }
            }
            else
            {
                // PowerPoint quirk: Application.Quit may complete cleanly while the process lingers
                // for seconds. Every handle already passed a successful Quit; for PROVEN-OWNED
                // instances a short exit poll followed by an identity-verified kill is safe (the
                // process provably belongs to this session and cannot contain user work).
                foreach (var handle in handles)
                {
                    TryTerminateWhenLingering(handle);
                }
            }
        }

        if (_ownsDispatcher)
        {
            _dispatcher.Dispose();
        }
    }
}

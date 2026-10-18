using System.Threading;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.App.ViewModels;

/// <summary>
/// Application/shell-level contract a page view model talks through. Pages never create
/// services on their own, never own the run token, and never write settings by themselves:
/// every shared responsibility lives on the shell so there is exactly one pipeline, one
/// run gate and one settings writer for the whole app.
/// </summary>
public interface IShellPageHost
{
    /// <summary>True while any run (convert batch or PDF tool) is active.</summary>
    bool IsRunActive { get; }

    /// <summary>
    /// Begins the exclusive single-run window owned by the caller. Returns null when a run
    /// is already active; disposing the lease ends the run exactly like the old inline
    /// finally blocks did.
    /// </summary>
    IRunLease? TryBeginRun();

    /// <summary>Cancels whichever run is currently active (convert batch or PDF tool).</summary>
    void RequestCancelRun();

    void Log(string message);

    /// <summary>Sets the top alert strip text and mirrors it onto the bottom summary.</summary>
    void ShowAlert(string message);

    /// <summary>Only clears the top alert strip (unlike <see cref="ShowAlert"/>).</summary>
    void ClearAlert();

    /// <summary>Replaces the shared bottom-bar summary line shown on every page.</summary>
    void SetSummaryText(string text);

    /// <summary>Appends a rejected path entry (deduplicated) to the shared rejected list.</summary>
    void AddRejectedPath(string path, string reason);

    /// <summary>Clears the shared rejected paths list (used when the queue is cleared).</summary>
    void ClearRejectedPaths();

    string? CustomLibreOfficePath { get; }

    bool EngineFallbackPreference { get; }

    /// <summary>Persists the engine fallback preference to disk (sets and saves).</summary>
    void PersistEngineFallbackPreference(bool enabled);

    /// <summary>
    /// The scanner instance created once by the shell; callers must not create a second one.
    /// </summary>
    IBatchInputScanner InputScanner { get; }

    /// <summary>Creates the batch runner for one conversion run (same as before the split).</summary>
    UiBatchRunner CreateBatchRunner();

    IReadOnlyList<string> PickInputFiles();

    string? PickFolder(string title);

    /// <summary>Returns the availability guidance line for the selected engine preference.</summary>
    string GetEngineGuidance(BatchConversionEnginePreference preference);

    /// <summary>Raised on the UI thread after an engine recheck refreshed availability.</summary>
    event EventHandler? EngineAvailabilityChanged;

    /// <summary>Raised when <c>IsRunning</c> changed (run started or ended).</summary>
    event EventHandler? RunStateChanged;
}

/// <summary>
/// Scoped handle for one conversion run: the caller holds the cancellation token and ends
/// the run (and the IsRunning gate) by disposing the lease.
/// </summary>
public interface IRunLease : IDisposable
{
    CancellationToken Token { get; }
}

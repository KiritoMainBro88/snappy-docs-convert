using SysProcess = System.Diagnostics.Process;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Verifiable identity of an Office process that this app created. Captured once when the handle is
/// created and re-checked immediately before any termination, so the OS reusing a PID for a
/// completely different process can never route a watchdog kill to an unrelated process.
/// </summary>
/// <param name="ProcessId">OS process id.</param>
/// <param name="ProcessName">Expected image name without extension (e.g. "WINWORD", "EXCEL", "POWERPNT").</param>
/// <param name="StartTimeUtc">Process start time used as the PID-reuse discriminator.</param>
public sealed record OfficeProcessIdentity(int ProcessId, string ProcessName, DateTime StartTimeUtc);

/// <summary>
/// Answers "is this still the same live process we recorded earlier?". Abstracted so the watchdog's
/// ownership decision is unit-testable without real OS processes.
/// </summary>
public interface IProcessVerifier
{
    bool IsCurrent(OfficeProcessIdentity identity);
}

/// <summary>
/// Default <see cref="IProcessVerifier"/>. A mismatch on id existence, image name, or recorded start
/// time means the PID was reused or the process exited: verification fails closed, and the caller
/// must NOT terminate the process. Runs on all platforms but only trusts process data on Windows.
/// </summary>
public sealed class WindowsProcessVerifier : IProcessVerifier
{
    /// <summary>Tolerance for DateTime tick rounding between the capture and the check.</summary>
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1.5);

    public bool IsCurrent(OfficeProcessIdentity identity)
    {
        if (!OperatingSystem.IsWindows())
        {
            // COM/Office automation (and therefore watchdog kills) is Windows-only.
            return false;
        }
        if (identity is null || identity.ProcessId <= 0 || string.IsNullOrWhiteSpace(identity.ProcessName))
        {
            return false;
        }

        try
        {
            using var process = SysProcess.GetProcessById(identity.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            if (!string.Equals(process.ProcessName, identity.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // StartTime can throw (access denied / exited mid-check). Not proving it means not owned.
            try
            {
                var started = process.StartTime.ToUniversalTime();
                return Math.Abs((started - identity.StartTimeUtc).TotalSeconds) <= StartTimeTolerance.TotalSeconds;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            // NoSuchProcess / ArgumentException / InvalidOperationException: PID gone or PID reuse.
            return false;
        }
    }
}

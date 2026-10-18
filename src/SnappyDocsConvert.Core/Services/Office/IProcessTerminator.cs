using SysProcess = System.Diagnostics.Process;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>Kills an OS process by id. Abstracted so the session's watchdog is unit-testable.</summary>
public interface IProcessTerminator
{
    void Kill(int processId);
}

public sealed class ProcessTerminator : IProcessTerminator
{
    public void Kill(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = SysProcess.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process already gone or inaccessible.
        }
    }
}

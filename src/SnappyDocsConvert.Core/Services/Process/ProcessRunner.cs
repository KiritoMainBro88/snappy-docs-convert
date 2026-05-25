using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;

namespace SnappyDocsConvert.Core.Services.Process;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await KillAndWaitAsync(process).ConfigureAwait(false);
            stopwatch.Stop();

            return new ProcessRunResult(
                null,
                true,
                await ReadCompletedOutputAsync(stdoutTask).ConfigureAwait(false),
                await ReadCompletedOutputAsync(stderrTask).ConfigureAwait(false),
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await KillAndWaitAsync(process).ConfigureAwait(false);
            throw;
        }

        stopwatch.Stop();

        return new ProcessRunResult(
            process.ExitCode,
            false,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false),
            stopwatch.Elapsed);
    }

    private static async Task KillAndWaitAsync(System.Diagnostics.Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
    }

    private static async Task<string> ReadCompletedOutputAsync(Task<string> outputTask)
    {
        try
        {
            return await outputTask.ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }
}

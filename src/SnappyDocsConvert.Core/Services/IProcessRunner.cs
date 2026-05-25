using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

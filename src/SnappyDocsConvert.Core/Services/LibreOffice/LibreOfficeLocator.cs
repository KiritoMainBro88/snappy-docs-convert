using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Paths;
using SnappyDocsConvert.Core.Services.Process;

namespace SnappyDocsConvert.Core.Services.LibreOffice;

public sealed class LibreOfficeLocator : ILibreOfficeLocator
{
    private static readonly string[] PathExecutableNames =
    {
        "soffice.com",
        "soffice.exe"
    };

    private static readonly string[] StandardWindowsPaths =
    {
        @"C:\Program Files\LibreOffice\program\soffice.com",
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.com",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    };

    private readonly IPathService _pathService;
    private readonly IProcessRunner _processRunner;

    public LibreOfficeLocator()
        : this(new PathService(), new ProcessRunner())
    {
    }

    public LibreOfficeLocator(IPathService pathService, IProcessRunner processRunner)
    {
        _pathService = pathService;
        _processRunner = processRunner;
    }

    public async Task<EngineAvailability> LocateAsync(
        LibreOfficeOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            return await AvailabilityForCandidateAsync(
                options.ExecutablePath,
                "User-provided LibreOffice executable path does not exist.",
                options,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var executableName in PathExecutableNames)
        {
            var pathCandidate = ResolveFromPath(executableName);
            if (pathCandidate is not null)
            {
                return await AvailableWithVersionAsync(pathCandidate, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var standardPath in StandardWindowsPaths)
        {
            if (_pathService.FileExists(standardPath))
            {
                return await AvailableWithVersionAsync(standardPath, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return EngineAvailability.Unavailable(
            "LibreOffice executable not found. Install LibreOffice or provide soffice.com/soffice.exe path.");
    }

    private async Task<EngineAvailability> AvailabilityForCandidateAsync(
        string candidate,
        string missingReason,
        LibreOfficeOptions options,
        CancellationToken cancellationToken)
    {
        var fullPath = _pathService.GetFullPath(candidate);
        if (!_pathService.FileExists(fullPath))
        {
            return EngineAvailability.Unavailable(missingReason);
        }

        return await AvailableWithVersionAsync(fullPath, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EngineAvailability> AvailableWithVersionAsync(
        string executablePath,
        LibreOfficeOptions options,
        CancellationToken cancellationToken)
    {
        var version = options.ProbeVersion
            ? await TryGetVersionAsync(executablePath, options.VersionProbeTimeout, cancellationToken)
                .ConfigureAwait(false)
            : null;

        return EngineAvailability.Available(executablePath, version);
    }

    private async Task<string?> TryGetVersionAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                executablePath,
                new[] { "--version" },
                timeout,
                cancellationToken).ConfigureAwait(false);

            var output = string.IsNullOrWhiteSpace(result.Stdout)
                ? result.Stderr
                : result.Stdout;

            return string.IsNullOrWhiteSpace(output)
                ? null
                : output.Trim().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveFromPath(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = directory.Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var candidate = _pathService.Combine(trimmed, executableName);
            if (_pathService.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

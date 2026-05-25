using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Paths;
using SnappyDocsConvert.Core.Services.Process;

namespace SnappyDocsConvert.Core.Services.LibreOffice;

public sealed class LibreOfficeConversionEngine : ILibreOfficeConversionEngine
{
    private const int SnippetLimit = 4000;

    private readonly ILibreOfficeLocator _locator;
    private readonly IProcessRunner _processRunner;
    private readonly IPathService _pathService;
    private readonly LibreOfficeCommandBuilder _commandBuilder;

    public LibreOfficeConversionEngine()
        : this(
            new LibreOfficeLocator(),
            new ProcessRunner(),
            new PathService(),
            new LibreOfficeCommandBuilder())
    {
    }

    public LibreOfficeConversionEngine(
        ILibreOfficeLocator locator,
        IProcessRunner processRunner,
        IPathService pathService,
        LibreOfficeCommandBuilder commandBuilder)
    {
        _locator = locator;
        _processRunner = processRunner;
        _pathService = pathService;
        _commandBuilder = commandBuilder;
    }

    public async Task<ConversionResult> ConvertToPdfAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        string? tempProfileDirectory = null;
        ProcessRunResult? processResult = null;

        try
        {
            if (string.IsNullOrWhiteSpace(request.InputPath))
            {
                return Failure(
                    request,
                    ConversionStatus.MissingInput,
                    "Input path is required.",
                    stopwatch.Elapsed);
            }

            var inputPath = _pathService.GetFullPath(request.InputPath);
            if (!_pathService.FileExists(inputPath))
            {
                return Failure(
                    request,
                    ConversionStatus.MissingInput,
                    "Input file does not exist.",
                    stopwatch.Elapsed);
            }

            var fileKind = _commandBuilder.GetFileKind(inputPath);
            if (fileKind == ConversionFileKind.Pdf)
            {
                return Failure(
                    request,
                    ConversionStatus.UnsupportedInput,
                    "PDF input is already PDF; Phase 2 LibreOffice engine does not convert PDF files.",
                    stopwatch.Elapsed);
            }

            if (!_commandBuilder.IsSupportedInput(inputPath))
            {
                return Failure(
                    request,
                    ConversionStatus.UnsupportedInput,
                    $"Unsupported input extension '{_pathService.GetExtension(inputPath)}'.",
                    stopwatch.Elapsed);
            }

            var availability = await _locator.LocateAsync(request.Options, cancellationToken)
                .ConfigureAwait(false);
            if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
            {
                return Failure(
                    request,
                    ConversionStatus.EngineUnavailable,
                    availability.Reason ?? "LibreOffice is unavailable.",
                    stopwatch.Elapsed);
            }

            var outputDirectory = _pathService.GetFullPath(request.OutputDirectory);
            _pathService.CreateDirectory(outputDirectory);

            var expectedPdfPath = _commandBuilder.GetExpectedPdfPath(inputPath, outputDirectory);
            if (_pathService.FileExists(expectedPdfPath) && !request.AllowOverwrite)
            {
                return Failure(
                    request,
                    ConversionStatus.OutputCollision,
                    $"Output PDF already exists: {expectedPdfPath}",
                    stopwatch.Elapsed);
            }

            tempProfileDirectory = CreateTempProfileDirectory();
            var command = _commandBuilder.BuildConvertToPdfCommand(
                availability.ExecutablePath,
                inputPath,
                outputDirectory,
                tempProfileDirectory);

            processResult = await _processRunner.RunAsync(
                command.ExecutablePath,
                command.Arguments,
                request.Timeout ?? request.Options.DefaultTimeout,
                cancellationToken).ConfigureAwait(false);

            if (processResult.TimedOut)
            {
                return Failure(
                    request,
                    ConversionStatus.TimedOut,
                    "LibreOffice conversion timed out.",
                    stopwatch.Elapsed,
                    processResult,
                    warnings);
            }

            if (processResult.ExitCode != 0)
            {
                return Failure(
                    request,
                    ConversionStatus.ProcessFailed,
                    $"LibreOffice exited with code {processResult.ExitCode}.",
                    stopwatch.Elapsed,
                    processResult,
                    warnings);
            }

            var actualPdfPath = ResolveProducedPdf(expectedPdfPath, outputDirectory, inputPath);
            if (actualPdfPath is null)
            {
                return Failure(
                    request,
                    ConversionStatus.OutputMissing,
                    "LibreOffice completed but did not produce the expected PDF.",
                    stopwatch.Elapsed,
                    processResult,
                    warnings);
            }

            return new ConversionResult
            {
                Success = true,
                Status = ConversionStatus.Succeeded,
                InputPath = inputPath,
                OutputPdfPath = actualPdfPath,
                EngineKind = ConversionEngineKind.LibreOffice,
                Duration = stopwatch.Elapsed,
                ExitCode = processResult.ExitCode,
                StdoutSnippet = Snippet(processResult.Stdout),
                StderrSnippet = Snippet(processResult.Stderr),
                Warnings = warnings
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request,
                ConversionStatus.Cancelled,
                "LibreOffice conversion was cancelled.",
                stopwatch.Elapsed,
                processResult,
                warnings);
        }
        finally
        {
            if (tempProfileDirectory is not null)
            {
                TryDeleteTempProfile(tempProfileDirectory, warnings);
            }
        }
    }

    private string CreateTempProfileDirectory()
    {
        var directory = _pathService.Combine(
            _pathService.GetTempPath(),
            "SnappyDocsConvert",
            "LibreOfficeProfiles",
            Guid.NewGuid().ToString("N"));

        _pathService.CreateDirectory(directory);
        return directory;
    }

    private string? ResolveProducedPdf(
        string expectedPdfPath,
        string outputDirectory,
        string inputPath)
    {
        if (_pathService.FileExists(expectedPdfPath))
        {
            return expectedPdfPath;
        }

        var expectedBaseName = _pathService.GetFileNameWithoutExtension(inputPath);
        return _pathService
            .EnumerateFiles(outputDirectory, "*.pdf")
            .FirstOrDefault(path =>
                string.Equals(
                    _pathService.GetFileNameWithoutExtension(path),
                    expectedBaseName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private void TryDeleteTempProfile(string tempProfileDirectory, List<string> warnings)
    {
        try
        {
            if (_pathService.DirectoryExists(tempProfileDirectory))
            {
                _pathService.DeleteDirectory(tempProfileDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Temp LibreOffice profile cleanup failed: {ex.Message}");
        }
    }

    private static ConversionResult Failure(
        ConversionRequest request,
        ConversionStatus status,
        string message,
        TimeSpan duration,
        ProcessRunResult? processResult = null,
        IReadOnlyList<string>? warnings = null)
        => new()
        {
            Success = false,
            Status = status,
            InputPath = request.InputPath,
            EngineKind = ConversionEngineKind.LibreOffice,
            Duration = duration,
            ExitCode = processResult?.ExitCode,
            StdoutSnippet = processResult is null ? null : Snippet(processResult.Stdout),
            StderrSnippet = processResult is null ? null : Snippet(processResult.Stderr),
            ErrorMessage = message,
            Warnings = warnings ?? Array.Empty<string>()
        };

    private static string? Snippet(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= SnippetLimit
            ? value
            : value[..SnippetLimit];
    }
}

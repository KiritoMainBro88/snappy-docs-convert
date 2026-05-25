using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Paths;

namespace SnappyDocsConvert.Core.Services.Office;

public sealed class OfficeComConversionEngine : IOfficeComConversionEngine
{
    private static readonly SemaphoreSlim OfficeComLock = new(1, 1);
    private readonly IMicrosoftOfficeAvailabilityProvider _availabilityProvider;
    private readonly IOfficeComRunner _officeComRunner;
    private readonly IPathService _pathService;
    private readonly OfficeConversionOptions _options;

    public OfficeComConversionEngine()
        : this(
            new MicrosoftOfficeAvailabilityProvider(),
            new OfficeComRunner(),
            new PathService(),
            new OfficeConversionOptions())
    {
    }

    public OfficeComConversionEngine(
        IMicrosoftOfficeAvailabilityProvider availabilityProvider,
        IOfficeComRunner officeComRunner,
        IPathService pathService,
        OfficeConversionOptions options)
    {
        _availabilityProvider = availabilityProvider;
        _officeComRunner = officeComRunner;
        _pathService = pathService;
        _options = options;
    }

    public async Task<ConversionResult> ConvertToPdfAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            return Failure(request, ConversionStatus.MissingInput, "Input path is required.", stopwatch.Elapsed);
        }

        var inputPath = _pathService.GetFullPath(request.InputPath);
        if (!_pathService.FileExists(inputPath))
        {
            return Failure(request, ConversionStatus.MissingInput, "Input file does not exist.", stopwatch.Elapsed);
        }

        var appKind = GetOfficeAppKind(inputPath);
        if (appKind is null)
        {
            return Failure(
                request,
                ConversionStatus.UnsupportedInput,
                $"Unsupported Office COM input extension '{_pathService.GetExtension(inputPath)}'.",
                stopwatch.Elapsed);
        }

        var availability = await _availabilityProvider
            .GetOfficeAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!CanUseApp(availability, appKind.Value))
        {
            return Failure(
                request,
                ConversionStatus.EngineUnavailable,
                RequiredAppMissingMessage(appKind.Value),
                stopwatch.Elapsed);
        }

        var outputDirectory = _pathService.GetFullPath(request.OutputDirectory);
        _pathService.CreateDirectory(outputDirectory);
        var outputPdfPath = _pathService.Combine(
            outputDirectory,
            $"{_pathService.GetFileNameWithoutExtension(inputPath)}.pdf");

        if (_pathService.FileExists(outputPdfPath) && !request.AllowOverwrite)
        {
            return Failure(
                request,
                ConversionStatus.OutputCollision,
                $"Output PDF already exists: {outputPdfPath}",
                stopwatch.Elapsed);
        }

        await OfficeComLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OfficeComExportResult exportResult;
        try
        {
            exportResult = await _officeComRunner.ExportToPdfAsync(
                appKind.Value,
                inputPath,
                outputPdfPath,
                request.Timeout ?? _options.DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request,
                ConversionStatus.Cancelled,
                "Microsoft Office COM conversion was cancelled.",
                stopwatch.Elapsed);
        }
        finally
        {
            OfficeComLock.Release();
        }

        if (exportResult.TimedOut)
        {
            return Failure(
                request,
                ConversionStatus.TimedOut,
                exportResult.ErrorMessage ?? "Microsoft Office COM export timed out.",
                stopwatch.Elapsed,
                exportResult.Warnings);
        }

        if (!exportResult.Success)
        {
            return Failure(
                request,
                ConversionStatus.ProcessFailed,
                exportResult.ErrorMessage ?? "Microsoft Office COM export failed.",
                stopwatch.Elapsed,
                exportResult.Warnings);
        }

        if (!_pathService.FileExists(outputPdfPath) || new FileInfo(outputPdfPath).Length <= 0)
        {
            return Failure(
                request,
                ConversionStatus.OutputMissing,
                "Microsoft Office COM export completed but did not produce a non-empty PDF.",
                stopwatch.Elapsed,
                exportResult.Warnings);
        }

        return new ConversionResult
        {
            Success = true,
            Status = ConversionStatus.Succeeded,
            InputPath = inputPath,
            OutputPdfPath = outputPdfPath,
            EngineKind = ConversionEngineKind.MicrosoftOffice,
            Duration = stopwatch.Elapsed,
            Warnings = exportResult.Warnings
        };
    }

    internal static OfficeAppKind? GetOfficeAppKind(string inputPath)
        => Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".doc" or ".docx" or ".rtf" => OfficeAppKind.Word,
            ".ppt" or ".pptx" => OfficeAppKind.PowerPoint,
            _ => null
        };

    private static bool CanUseApp(OfficeAvailability availability, OfficeAppKind appKind)
        => appKind switch
        {
            OfficeAppKind.Word => availability.CanConvertWordDocuments,
            OfficeAppKind.PowerPoint => availability.CanConvertPowerPointPresentations,
            _ => false
        };

    private static string RequiredAppMissingMessage(OfficeAppKind appKind)
        => appKind switch
        {
            OfficeAppKind.Word => "Microsoft Word COM is not available for Word document export.",
            OfficeAppKind.PowerPoint => "Microsoft PowerPoint COM is not available for presentation export.",
            _ => "Required Microsoft Office COM app is not available."
        };

    private static ConversionResult Failure(
        ConversionRequest request,
        ConversionStatus status,
        string message,
        TimeSpan duration,
        IReadOnlyList<string>? warnings = null)
        => new()
        {
            Success = false,
            Status = status,
            InputPath = request.InputPath,
            EngineKind = ConversionEngineKind.MicrosoftOffice,
            Duration = duration,
            ErrorMessage = message,
            Warnings = warnings ?? Array.Empty<string>()
        };
}

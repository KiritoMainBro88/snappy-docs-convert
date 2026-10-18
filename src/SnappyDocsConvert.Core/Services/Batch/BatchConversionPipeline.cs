using System.Diagnostics;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.LibreOffice;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Pdf;

namespace SnappyDocsConvert.Core.Services.Batch;

public sealed class BatchConversionPipeline : IBatchConversionPipeline
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".rtf",
        ".odt",
        ".ppt",
        ".pptx",
        ".odp",
        ".xls",
        ".xlsx",
        ".csv",
        ".ods"
    };

    // A per-file failure where retrying with the other engine could plausibly succeed
    // (Requirement 2.2). Explicitly excludes Cancelled, MissingInput, UnsupportedInput,
    // and OutputCollision, which are non-recoverable and never trigger fallback.
    private static readonly HashSet<ConversionStatus> RecoverableFailures = new()
    {
        ConversionStatus.Failed,
        ConversionStatus.ProcessFailed,
        ConversionStatus.TimedOut,
        ConversionStatus.OutputMissing,
        ConversionStatus.EngineUnavailable
    };

    private readonly IBatchOutputPlanner _outputPlanner;
    private readonly IConversionEngineSelector _engineSelector;
    private readonly ILibreOfficeConversionEngine _libreOfficeEngine;
    private readonly IOfficeComConversionEngine _officeComEngine;
    private readonly IPdfImageRenderer _pdfImageRenderer;

    public BatchConversionPipeline()
        : this(
            new BatchOutputPlanner(),
            new ConversionEngineSelector(),
            new LibreOfficeConversionEngine(),
            new OfficeComConversionEngine(),
            new PdfToImageRenderer())
    {
    }

    public BatchConversionPipeline(
        IBatchOutputPlanner outputPlanner,
        IConversionEngineSelector engineSelector,
        ILibreOfficeConversionEngine libreOfficeEngine,
        IOfficeComConversionEngine officeComEngine,
        IPdfImageRenderer pdfImageRenderer)
    {
        _outputPlanner = outputPlanner;
        _engineSelector = engineSelector;
        _libreOfficeEngine = libreOfficeEngine;
        _officeComEngine = officeComEngine;
        _pdfImageRenderer = pdfImageRenderer;
    }

    public async Task<BatchConversionResult> RunAsync(
        BatchConversionJob job,
        IProgress<BatchConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<BatchConversionItemResult>();

        for (var index = 0; index < job.Items.Count; index++)
        {
            var item = job.Items[index];
            if (cancellationToken.IsCancellationRequested)
            {
                AddCancelledResults(job, results, index);
                break;
            }

            Report(progress, job, item, index, BatchConversionItemStatus.Running, "Running");

            var result = await ProcessItemAsync(job, item, progress, index, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            Report(progress, job, item, index, result.Status, result.ErrorMessage ?? result.Status.ToString());

            if (result.Status == BatchConversionItemStatus.Cancelled)
            {
                AddCancelledResults(job, results, index + 1);
                break;
            }
        }

        return new BatchConversionResult
        {
            Items = results,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<BatchConversionItemResult> ProcessItemAsync(
        BatchConversionJob job,
        BatchConversionItem item,
        IProgress<BatchConversionProgress>? progress,
        int index,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        string? tempDirectory = null;

        try
        {
            if (string.IsNullOrWhiteSpace(job.Options.OutputRoot))
            {
                return Failed(item.InputPath, "Batch output root is required.", stopwatch.Elapsed, warnings);
            }

            if (string.IsNullOrWhiteSpace(item.InputPath))
            {
                return Failed(item.InputPath, "Input path is required.", stopwatch.Elapsed, warnings);
            }

            var inputPath = Path.GetFullPath(item.InputPath);
            if (!File.Exists(inputPath))
            {
                return Failed(inputPath, "Input file does not exist.", stopwatch.Elapsed, warnings);
            }

            var extension = Path.GetExtension(inputPath);
            if (!SupportedExtensions.Contains(extension))
            {
                return Failed(inputPath, $"Unsupported batch input extension '{extension}'.", stopwatch.Elapsed, warnings);
            }

            var plan = _outputPlanner.PlanItem(new BatchConversionItem(inputPath), job.Options);
            var isPdfInput = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
            var targetIncludesPdf = IncludesPdf(job.Options.Target);
            var targetIncludesImages = IncludesImages(job.Options.Target);
            string? sourcePdfPath;
            string? outputPdfPath = null;
            ConversionEngineKind? documentEngine = null;
            BatchFallbackInfo? fallbackInfo = null;

            if (isPdfInput)
            {
                sourcePdfPath = inputPath;
                if (targetIncludesPdf)
                {
                    var copyResult = CopyPdfToOutput(inputPath, plan.PdfOutputPath, job.Options.OverwritePolicy, warnings);
                    outputPdfPath = copyResult.Path;
                    sourcePdfPath = copyResult.Path;

                    if (copyResult.Skipped && !targetIncludesImages)
                    {
                        return Skipped(inputPath, plan, outputPdfPath, stopwatch.Elapsed, warnings);
                    }
                }
            }
            else
            {
                var selection = await _engineSelector
                    .SelectAsync(inputPath, job.Options, cancellationToken)
                    .ConfigureAwait(false);

                if (!selection.Success || selection.EngineKind is null)
                {
                    return Failed(
                        inputPath,
                        SelectionError(selection),
                        stopwatch.Elapsed,
                        warnings.Concat(selection.Recommendations).ToArray(),
                        plan);
                }

                var primaryEngine = selection.EngineKind.Value;
                tempDirectory = CreateTempDirectory();

                var attempt = await RunWithFallbackAsync(
                    primaryEngine,
                    inputPath,
                    tempDirectory,
                    job,
                    item,
                    index,
                    progress,
                    warnings,
                    cancellationToken).ConfigureAwait(false);

                documentEngine = attempt.EngineUsed;
                fallbackInfo = attempt.Fallback;

                if (attempt.Cancelled)
                {
                    return Cancelled(inputPath, stopwatch.Elapsed, warnings);
                }

                var conversion = attempt.Result;
                if (!conversion.Success || string.IsNullOrWhiteSpace(conversion.OutputPdfPath))
                {
                    return Failed(
                        inputPath,
                        conversion.ErrorMessage ?? "Document conversion failed.",
                        stopwatch.Elapsed,
                        warnings,
                        plan,
                        documentEngine,
                        fallback: fallbackInfo);
                }

                if (!File.Exists(conversion.OutputPdfPath) || new FileInfo(conversion.OutputPdfPath).Length <= 0)
                {
                    return Failed(
                        inputPath,
                        "Document conversion did not produce a non-empty PDF.",
                        stopwatch.Elapsed,
                        warnings,
                        plan,
                        documentEngine,
                        fallback: fallbackInfo);
                }

                sourcePdfPath = conversion.OutputPdfPath;
                if (targetIncludesPdf || job.Options.KeepIntermediatePdf)
                {
                    var copyResult = CopyPdfToOutput(
                        conversion.OutputPdfPath,
                        plan.PdfOutputPath,
                        job.Options.OverwritePolicy,
                        warnings);
                    outputPdfPath = copyResult.Path;
                    sourcePdfPath = copyResult.Path;
                }
            }

            if (targetIncludesImages)
            {
                var renderResult = await _pdfImageRenderer.RenderAsync(
                    new PdfRenderRequest(sourcePdfPath, plan.ImageOutputDirectory)
                    {
                        Options = new PdfRenderOptions
                        {
                            Dpi = job.Options.Dpi,
                            Format = job.Options.ImageFormat,
                            JpegQuality = job.Options.JpegQuality,
                            OverwritePolicy = job.Options.OverwritePolicy,
                            PagePrefix = plan.PagePrefix
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                warnings.AddRange(renderResult.Warnings);
                if (!renderResult.Success)
                {
                    return Failed(
                        inputPath,
                        renderResult.ErrorMessage ?? "PDF image rendering failed.",
                        stopwatch.Elapsed,
                        warnings,
                        plan,
                        documentEngine,
                        outputPdfPath,
                        renderResult.OutputDirectory,
                        renderResult.OutputFiles,
                        fallbackInfo);
                }

                return Succeeded(
                    inputPath,
                    plan,
                    stopwatch.Elapsed,
                    warnings,
                    documentEngine,
                    outputPdfPath,
                    renderResult.OutputDirectory,
                    renderResult.OutputFiles,
                    fallbackInfo);
            }

            return Succeeded(
                inputPath,
                plan,
                stopwatch.Elapsed,
                warnings,
                documentEngine,
                outputPdfPath,
                null,
                Array.Empty<string>(),
                fallbackInfo);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new BatchConversionItemResult
            {
                InputPath = item.InputPath,
                Status = BatchConversionItemStatus.Cancelled,
                ErrorMessage = "Batch conversion was cancelled.",
                Warnings = warnings,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return Failed(item.InputPath, ex.Message, stopwatch.Elapsed, warnings);
        }
        finally
        {
            if (tempDirectory is not null)
            {
                TryDeleteTempDirectory(tempDirectory);
            }
        }
    }

    /// <summary>
    /// Runs the primary engine into a per-attempt <c>temp/primary</c> subdirectory and, on a
    /// recoverable failure with fallback enabled and a distinct eligible alternate engine, retries
    /// the same file once with that alternate into <c>temp/fallback</c> using the same
    /// <see cref="CancellationToken"/>. Returns the chosen <see cref="ConversionResult"/>, the engine
    /// used (or last attempted), a cancellation flag, and optional fallback metadata. Appends neutral
    /// English fallback warnings to <paramref name="warnings"/> and emits a fallback-attempt progress
    /// report before the fallback runs.
    /// </summary>
    private async Task<FallbackAttempt> RunWithFallbackAsync(
        ConversionEngineKind primaryEngine,
        string inputPath,
        string tempRoot,
        BatchConversionJob job,
        BatchConversionItem item,
        int index,
        IProgress<BatchConversionProgress>? progress,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        // 1. Run the primary engine into its own subdirectory so a partial primary output can never
        //    be mistaken for a fallback success.
        var primaryDirectory = Path.Combine(tempRoot, "primary");
        Directory.CreateDirectory(primaryDirectory);
        var primary = await ConvertDocumentAsync(
            primaryEngine,
            inputPath,
            primaryDirectory,
            job.Options,
            cancellationToken).ConfigureAwait(false);
        warnings.AddRange(primary.Warnings);

        // 2. Cancellation by the primary is never recoverable (Req 2.1).
        if (primary.Status == ConversionStatus.Cancelled)
        {
            return FallbackAttempt.CancelledAttempt(primaryEngine);
        }

        // 3. Primary success with a usable PDF short-circuits before any fallback (Req 1.3).
        if (HasUsablePdf(primary))
        {
            return FallbackAttempt.From(primary, primaryEngine);
        }

        // 4. Non-recoverable failures are reported as-is without fallback (Req 2.2, 2.3, 2.4).
        if (!IsRecoverable(primary))
        {
            return FallbackAttempt.From(primary, primaryEngine);
        }

        // 5. Honour the fallback setting (Req 4.4).
        if (!job.Options.EnableEngineFallback)
        {
            return FallbackAttempt.From(primary, primaryEngine);
        }

        // 6. Choose the first distinct eligible alternate engine (Req 3.3, 3.4).
        var eligibility = await _engineSelector
            .GetEligibleEnginesAsync(inputPath, job.Options, cancellationToken)
            .ConfigureAwait(false);
        ConversionEngineKind? fallbackEngine = null;
        foreach (var kind in eligibility.EligibleEngines)
        {
            if (kind != primaryEngine)
            {
                fallbackEngine = kind;
                break;
            }
        }

        if (fallbackEngine is null)
        {
            // No eligible alternate: report the primary failure unchanged (Req 3.3).
            return FallbackAttempt.From(primary, primaryEngine);
        }

        // 7. Honour cancellation requested before the fallback attempt begins (Req 6.1).
        if (cancellationToken.IsCancellationRequested)
        {
            return FallbackAttempt.CancelledAttempt(primaryEngine);
        }

        // 8. Record the fallback attempt and surface it to the live UI (Req 5.1, 5.4).
        var primaryReason = primary.ErrorMessage ?? "Document conversion failed.";
        warnings.Add(
            $"Fallback: {primaryEngine} failed ({primaryReason}); retrying with {fallbackEngine.Value}.");
        Report(
            progress,
            job,
            item,
            index,
            BatchConversionItemStatus.Running,
            $"Fallback: {primaryEngine} failed; retrying with {fallbackEngine.Value}.");

        // Run the fallback into its own subdirectory with the SAME cancellation token (Req 6.2, 7.1).
        var fallbackDirectory = Path.Combine(tempRoot, "fallback");
        Directory.CreateDirectory(fallbackDirectory);
        var fallback = await ConvertDocumentAsync(
            fallbackEngine.Value,
            inputPath,
            fallbackDirectory,
            job.Options,
            cancellationToken).ConfigureAwait(false);
        warnings.AddRange(fallback.Warnings);

        // 9. Cancellation during the fallback is surfaced as Cancelled, not a failure (Req 6.3).
        if (fallback.Status == ConversionStatus.Cancelled)
        {
            return FallbackAttempt.CancelledAttempt(fallbackEngine.Value);
        }

        // 10. Fallback success: use the fallback PDF and engine (Req 1.2, 5.2).
        if (HasUsablePdf(fallback))
        {
            warnings.Add($"Fallback: {fallbackEngine.Value} produced the PDF.");
            var successInfo = new BatchFallbackInfo
            {
                PrimaryEngine = primaryEngine,
                PrimaryFailureReason = primaryReason,
                FallbackEngine = fallbackEngine.Value,
                FallbackSucceeded = true
            };
            return FallbackAttempt.From(fallback, fallbackEngine.Value, successInfo);
        }

        // 11. Both engines failed: retain the PRIMARY failure reason as authoritative (Req 5.3).
        warnings.Add("Fallback: both engines failed.");
        var fallbackReason = fallback.ErrorMessage ?? "Document conversion failed.";
        warnings.Add($"Fallback: {fallbackEngine.Value} also failed ({fallbackReason}).");
        var failureInfo = new BatchFallbackInfo
        {
            PrimaryEngine = primaryEngine,
            PrimaryFailureReason = primaryReason,
            FallbackEngine = fallbackEngine.Value,
            FallbackSucceeded = false
        };
        // Retain the primary reason as the authoritative ErrorMessage; DocumentEngine reflects the
        // last engine attempted (the fallback) per Req 5.5.
        var retained = primary with { EngineKind = fallbackEngine.Value };
        return FallbackAttempt.From(retained, fallbackEngine.Value, failureInfo);
    }

    private static bool IsRecoverable(ConversionResult result)
        => !result.Success && RecoverableFailures.Contains(result.Status);

    private static bool HasUsablePdf(ConversionResult result)
        => result.Success && !string.IsNullOrWhiteSpace(result.OutputPdfPath);

    private async Task<ConversionResult> ConvertDocumentAsync(
        ConversionEngineKind engineKind,
        string inputPath,
        string outputDirectory,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        var request = new ConversionRequest(inputPath, outputDirectory)
        {
            AllowOverwrite = true,
            Options = new LibreOfficeOptions
            {
                ExecutablePath = options.LibreOfficeExecutablePath
            }
        };

        return engineKind switch
        {
            ConversionEngineKind.MicrosoftOffice => await _officeComEngine
                .ConvertToPdfAsync(request, cancellationToken)
                .ConfigureAwait(false),
            _ => await _libreOfficeEngine
                .ConvertToPdfAsync(request, cancellationToken)
                .ConfigureAwait(false)
        };
    }

    private static CopyPdfResult CopyPdfToOutput(
        string sourcePdfPath,
        string plannedOutputPath,
        OverwritePolicy overwritePolicy,
        List<string> warnings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(plannedOutputPath)!);
        var outputPath = ResolveOutputPath(plannedOutputPath, overwritePolicy, warnings, out var skipped);

        if (skipped)
        {
            return new CopyPdfResult(outputPath, true);
        }

        if (!PathsEqual(sourcePdfPath, outputPath))
        {
            File.Copy(sourcePdfPath, outputPath, overwrite: overwritePolicy == OverwritePolicy.Overwrite);
        }

        return new CopyPdfResult(outputPath, false);
    }

    private static string ResolveOutputPath(
        string plannedPath,
        OverwritePolicy overwritePolicy,
        List<string> warnings,
        out bool skipped)
    {
        skipped = false;
        if (!File.Exists(plannedPath))
        {
            return plannedPath;
        }

        if (overwritePolicy == OverwritePolicy.Overwrite)
        {
            return plannedPath;
        }

        if (overwritePolicy == OverwritePolicy.Skip)
        {
            skipped = true;
            warnings.Add($"Skipped existing PDF output: {plannedPath}");
            return plannedPath;
        }

        var directory = Path.GetDirectoryName(plannedPath)!;
        var baseName = Path.GetFileNameWithoutExtension(plannedPath);
        var extension = Path.GetExtension(plannedPath);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not create unique PDF output name for {plannedPath}.");
    }

    private static void AddCancelledResults(
        BatchConversionJob job,
        List<BatchConversionItemResult> results,
        int startIndex)
    {
        for (var index = startIndex; index < job.Items.Count; index++)
        {
            results.Add(new BatchConversionItemResult
            {
                InputPath = job.Items[index].InputPath,
                Status = BatchConversionItemStatus.Cancelled,
                ErrorMessage = "Batch conversion was cancelled."
            });
        }
    }

    private static void Report(
        IProgress<BatchConversionProgress>? progress,
        BatchConversionJob job,
        BatchConversionItem item,
        int index,
        BatchConversionItemStatus status,
        string? message)
    {
        progress?.Report(new BatchConversionProgress
        {
            TotalItems = job.Items.Count,
            CurrentItemIndex = index,
            CurrentFile = item.InputPath,
            Status = status,
            Message = message,
            PercentComplete = job.Items.Count == 0 ? 100 : (double)(index + 1) / job.Items.Count * 100
        });
    }

    private static BatchConversionItemResult Succeeded(
        string inputPath,
        BatchOutputItemPlan plan,
        TimeSpan duration,
        IReadOnlyList<string> warnings,
        ConversionEngineKind? documentEngine,
        string? outputPdfPath,
        string? imageOutputDirectory,
        IReadOnlyList<string> imageFiles,
        BatchFallbackInfo? fallback = null)
        => new()
        {
            InputPath = inputPath,
            Status = BatchConversionItemStatus.Succeeded,
            DocumentEngine = documentEngine,
            OutputPdfPath = outputPdfPath,
            ImageOutputDirectory = imageOutputDirectory,
            ImageFiles = imageFiles,
            Warnings = warnings,
            OutputPlan = plan,
            Fallback = fallback,
            Duration = duration
        };

    private static BatchConversionItemResult Cancelled(
        string inputPath,
        TimeSpan duration,
        IReadOnlyList<string> warnings)
        => new()
        {
            InputPath = inputPath,
            Status = BatchConversionItemStatus.Cancelled,
            ErrorMessage = "Batch conversion was cancelled.",
            Warnings = warnings,
            Duration = duration
        };

    private static BatchConversionItemResult Skipped(
        string inputPath,
        BatchOutputItemPlan plan,
        string outputPdfPath,
        TimeSpan duration,
        IReadOnlyList<string> warnings)
        => new()
        {
            InputPath = inputPath,
            Status = BatchConversionItemStatus.Skipped,
            OutputPdfPath = outputPdfPath,
            Warnings = warnings,
            OutputPlan = plan,
            Duration = duration
        };

    private static BatchConversionItemResult Failed(
        string inputPath,
        string errorMessage,
        TimeSpan duration,
        IReadOnlyList<string> warnings,
        BatchOutputItemPlan? plan = null,
        ConversionEngineKind? documentEngine = null,
        string? outputPdfPath = null,
        string? imageOutputDirectory = null,
        IReadOnlyList<string>? imageFiles = null,
        BatchFallbackInfo? fallback = null)
        => new()
        {
            InputPath = inputPath,
            Status = BatchConversionItemStatus.Failed,
            DocumentEngine = documentEngine,
            OutputPdfPath = outputPdfPath,
            ImageOutputDirectory = imageOutputDirectory,
            ImageFiles = imageFiles ?? Array.Empty<string>(),
            ErrorMessage = errorMessage,
            Warnings = warnings,
            OutputPlan = plan,
            Fallback = fallback,
            Duration = duration
        };

    private static string SelectionError(BatchEngineSelectionResult selection)
        => selection.ErrorMessage ?? "No document conversion engine is available. Check engine setup.";

    private static bool IncludesPdf(BatchConversionTarget target)
        => target is BatchConversionTarget.Pdf or BatchConversionTarget.PdfAndImages;

    private static bool IncludesImages(BatchConversionTarget target)
        => target is BatchConversionTarget.Images or BatchConversionTarget.PdfAndImages;

    private static bool PathsEqual(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "SnappyDocsConvert",
            "Batch",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void TryDeleteTempDirectory(string tempDirectory)
    {
        try
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var fullPath = Path.GetFullPath(tempDirectory);
            if (fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup; conversion result already carries real success/failure.
        }
    }

    private sealed record CopyPdfResult(string Path, bool Skipped);

    /// <summary>
    /// Outcome of <see cref="RunWithFallbackAsync"/>: the chosen conversion result, the engine that
    /// produced it (or the last engine attempted), whether the attempt ended in cancellation, and
    /// optional structured fallback metadata when a fallback was attempted.
    /// </summary>
    private sealed record FallbackAttempt(
        ConversionResult Result,
        ConversionEngineKind EngineUsed,
        bool Cancelled,
        BatchFallbackInfo? Fallback)
    {
        public static FallbackAttempt From(
            ConversionResult result,
            ConversionEngineKind engineUsed,
            BatchFallbackInfo? fallback = null)
            => new(result, engineUsed, false, fallback);

        public static FallbackAttempt CancelledAttempt(ConversionEngineKind engineUsed)
            => new(CancelledResult(engineUsed), engineUsed, true, null);

        private static ConversionResult CancelledResult(ConversionEngineKind engineUsed)
            => new()
            {
                Success = false,
                Status = ConversionStatus.Cancelled,
                InputPath = string.Empty,
                EngineKind = engineUsed
            };
    }
}

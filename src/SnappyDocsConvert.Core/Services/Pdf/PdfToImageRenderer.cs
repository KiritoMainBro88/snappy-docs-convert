using System.Diagnostics;
using PDFtoImage;
using SkiaSharp;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Paths;

namespace SnappyDocsConvert.Core.Services.Pdf;

public sealed class PdfToImageRenderer : IPdfImageRenderer
{
    private const int MinimumDpi = 72;
    private const int MaximumDpi = 600;
    private const int HighDpiWarningThreshold = 300;
    private static readonly SemaphoreSlim PdfiumLock = new(1, 1);

    private readonly IPathService _pathService;
    private readonly PdfImageOutputPlanner _outputPlanner;

    public PdfToImageRenderer()
        : this(new PathService(), new PdfImageOutputPlanner())
    {
    }

    internal PdfToImageRenderer(IPathService pathService, PdfImageOutputPlanner outputPlanner)
    {
        _pathService = pathService;
        _outputPlanner = outputPlanner;
    }

    public async Task<PdfRenderResult> RenderAsync(
        PdfRenderRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        var outputFiles = new List<string>();
        var pageResults = new List<PdfPageRenderResult>();

        if (string.IsNullOrWhiteSpace(request.InputPdfPath))
        {
            return Failure(request, "Input PDF path is required.", stopwatch.Elapsed);
        }

        var inputPath = _pathService.GetFullPath(request.InputPdfPath);
        if (!_pathService.FileExists(inputPath))
        {
            return Failure(request, "Input PDF file does not exist.", stopwatch.Elapsed);
        }

        if (!string.Equals(_pathService.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(request, "Input file must use the .pdf extension.", stopwatch.Elapsed);
        }

        var options = request.Options;
        var validationError = ValidateOptions(options, warnings);
        if (validationError is not null)
        {
            return Failure(request, validationError, stopwatch.Elapsed, warnings);
        }

        var outputDirectory = _pathService.GetFullPath(request.OutputDirectory);
        _pathService.CreateDirectory(outputDirectory);
        var pagePrefix = _outputPlanner.SanitizePrefix(options.PagePrefix);

        var lockTaken = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PdfiumLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;

            var pageCount = GetPageCount(inputPath);
            var startPage = options.StartPage ?? 1;
            var endPage = options.EndPage ?? pageCount;
            var pageRangeError = ValidatePageRange(startPage, endPage, pageCount);
            if (pageRangeError is not null)
            {
                return Failure(request, pageRangeError, stopwatch.Elapsed, warnings);
            }

            var renderOptions = new RenderOptions(
                Dpi: options.Dpi,
                WithAnnotations: true,
                BackgroundColor: SKColors.White);

            for (var pageNumber = startPage; pageNumber <= endPage; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outputPath = _outputPlanner.PlanOutputPath(
                    outputDirectory,
                    pagePrefix,
                    pageNumber,
                    pageCount,
                    options.Format,
                    options.OverwritePolicy);

                if (options.OverwritePolicy is OverwritePolicy.Skip && _pathService.FileExists(outputPath))
                {
                    var warning = $"Skipped existing output file: {outputPath}";
                    warnings.Add(warning);
                    outputFiles.Add(outputPath);
                    pageResults.Add(new PdfPageRenderResult
                    {
                        PageNumber = pageNumber,
                        OutputPath = outputPath,
                        Skipped = true,
                        Warning = warning
                    });
                    continue;
                }

                try
                {
                    RenderPage(inputPath, outputPath, pageNumber - 1, options, renderOptions);
                }
                catch (Exception ex) when (options.Format is ImageOutputFormat.Webp)
                {
                    return Failure(
                        request,
                        $"PDF page {pageNumber} could not be rendered as WebP. WebP may be unsupported by the current runtime. {ex.Message}",
                        stopwatch.Elapsed,
                        warnings,
                        outputFiles,
                        pageResults);
                }
                catch (Exception ex)
                {
                    return Failure(
                        request,
                        $"PDF page {pageNumber} render failed. {ex.Message}",
                        stopwatch.Elapsed,
                        warnings,
                        outputFiles,
                        pageResults);
                }

                if (!_pathService.FileExists(outputPath) || new FileInfo(outputPath).Length <= 0)
                {
                    return Failure(
                        request,
                        $"PDF page {pageNumber} render completed but output image was missing or empty.",
                        stopwatch.Elapsed,
                        warnings,
                        outputFiles,
                        pageResults);
                }

                outputFiles.Add(outputPath);
                pageResults.Add(new PdfPageRenderResult
                {
                    PageNumber = pageNumber,
                    OutputPath = outputPath
                });
            }

            return new PdfRenderResult
            {
                Success = true,
                InputPdfPath = inputPath,
                OutputDirectory = outputDirectory,
                PagesRendered = pageResults.Count(page => !page.Skipped),
                OutputFiles = outputFiles,
                PageResults = pageResults,
                Duration = stopwatch.Elapsed,
                Warnings = warnings
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(request, "PDF image rendering was cancelled.", stopwatch.Elapsed, warnings, outputFiles, pageResults);
        }
        catch (Exception ex)
        {
            return Failure(request, $"PDF image rendering failed. {ex.Message}", stopwatch.Elapsed, warnings, outputFiles, pageResults);
        }
        finally
        {
            if (lockTaken)
            {
                PdfiumLock.Release();
            }
        }
    }

    private static string? ValidateOptions(PdfRenderOptions options, List<string> warnings)
    {
        if (options.Dpi is < MinimumDpi or > MaximumDpi)
        {
            return $"DPI must be between {MinimumDpi} and {MaximumDpi}.";
        }

        if (options.Dpi > HighDpiWarningThreshold)
        {
            warnings.Add("High DPI can use significant memory and disk space.");
        }

        if (options.JpegQuality is < 1 or > 100)
        {
            return "JPEG quality must be between 1 and 100.";
        }

        return null;
    }

    private static string? ValidatePageRange(int startPage, int endPage, int pageCount)
    {
        if (pageCount < 1)
        {
            return "PDF does not contain any pages.";
        }

        if (startPage < 1)
        {
            return "StartPage must be 1 or greater.";
        }

        if (endPage > pageCount)
        {
            return $"EndPage cannot exceed PDF page count ({pageCount}).";
        }

        if (startPage > endPage)
        {
            return "StartPage cannot be greater than EndPage.";
        }

        return null;
    }

    private static int GetPageCount(string inputPath)
    {
        using var pdfStream = File.OpenRead(inputPath);
#pragma warning disable CA1416
        return Conversion.GetPageCount(pdfStream, leaveOpen: false);
#pragma warning restore CA1416
    }

    private static void RenderPage(
        string inputPath,
        string outputPath,
        int zeroBasedPageIndex,
        PdfRenderOptions options,
        RenderOptions renderOptions)
    {
        using var pdfStream = File.OpenRead(inputPath);
#pragma warning disable CA1416
        using var bitmap = Conversion.ToImage(
            pdfStream,
            page: zeroBasedPageIndex,
            leaveOpen: false,
            options: renderOptions);
#pragma warning restore CA1416
        using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var encoded = bitmap.Encode(
            outputStream,
            ToSkiaFormat(options.Format),
            QualityFor(options));

        if (!encoded)
        {
            throw new InvalidOperationException($"SkiaSharp could not encode {options.Format} output.");
        }
    }

    private static SKEncodedImageFormat ToSkiaFormat(ImageOutputFormat format)
        => format switch
        {
            ImageOutputFormat.Png => SKEncodedImageFormat.Png,
            ImageOutputFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageOutputFormat.Webp => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };

    private static int QualityFor(PdfRenderOptions options)
        => options.Format switch
        {
            ImageOutputFormat.Jpeg or ImageOutputFormat.Webp => options.JpegQuality,
            _ => 100
        };

    private static PdfRenderResult Failure(
        PdfRenderRequest request,
        string message,
        TimeSpan duration,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? outputFiles = null,
        IReadOnlyList<PdfPageRenderResult>? pageResults = null)
        => new()
        {
            Success = false,
            InputPdfPath = request.InputPdfPath,
            OutputDirectory = request.OutputDirectory,
            Duration = duration,
            ErrorMessage = message,
            Warnings = warnings ?? Array.Empty<string>(),
            OutputFiles = outputFiles ?? Array.Empty<string>(),
            PageResults = pageResults ?? Array.Empty<PdfPageRenderResult>()
        };
}

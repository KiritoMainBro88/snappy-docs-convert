using System.Security.Cryptography;
using System.Text;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Ocr;

/// <summary>Outcome of an OCR document run.</summary>
public sealed record OcrDocumentResult(bool Success, string OutputTextPath, string Text, string? ErrorMessage)
{
    public static OcrDocumentResult Ok(string path, string text) => new(true, path, text, null);
    public static OcrDocumentResult Failed(string error) => new(false, string.Empty, string.Empty, error);
}

/// <summary>
/// Orchestrates OCR over an image or a PDF (PDF pages are rasterised via the existing renderer, then
/// each page image is OCR'd and the text concatenated). Engine work is delegated to
/// <see cref="IOcrEngine"/>; PDF rasterisation to <see cref="IPdfImageRenderer"/>. Both are injected
/// so the orchestration is unit-testable with fakes (no PaddleOCR installed).
/// </summary>
public sealed class OcrDocumentService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"
    };

    private readonly IOcrEngine _engine;
    private readonly IPdfImageRenderer _renderer;

    public OcrDocumentService(IOcrEngine engine, IPdfImageRenderer renderer)
    {
        _engine = engine;
        _renderer = renderer;
    }

    public async Task<OcrDocumentResult> RecognizeAsync(
        string inputPath,
        string outputDirectory,
        int dpi,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return OcrDocumentResult.Failed("Input file does not exist.");
        }

        if (!await _engine.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return OcrDocumentResult.Failed("No OCR engine found. Install PaddleOCR and set its path.");
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = Path.GetExtension(inputPath);
        var outputTextPath = ResolveTextOutputPath(outputDirectory, inputPath);

        if (ImageExtensions.Contains(extension))
        {
            var result = await _engine.RecognizeImageAsync(inputPath, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                return OcrDocumentResult.Failed(result.ErrorMessage ?? "OCR failed.");
            }

            await File.WriteAllTextAsync(outputTextPath, result.Text, cancellationToken).ConfigureAwait(false);
            return OcrDocumentResult.Ok(outputTextPath, result.Text);
        }

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await RecognizePdfAsync(inputPath, outputDirectory, outputTextPath, dpi, cancellationToken)
                .ConfigureAwait(false);
        }

        return OcrDocumentResult.Failed($"OCR does not support '{extension}'. Use an image or PDF.");
    }

    private async Task<OcrDocumentResult> RecognizePdfAsync(
        string inputPath,
        string outputDirectory,
        string outputTextPath,
        int dpi,
        CancellationToken cancellationToken)
    {
        var pageDirectory = Path.Combine(outputDirectory, $"ocr-pages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pageDirectory);

        try
        {
            var render = await _renderer.RenderAsync(
                new PdfRenderRequest(inputPath, pageDirectory)
                {
                    Options = new PdfRenderOptions
                    {
                        Dpi = dpi <= 0 ? 200 : dpi,
                        Format = ImageOutputFormat.Png,
                        PagePrefix = "page"
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (!render.Success)
            {
                return OcrDocumentResult.Failed(render.ErrorMessage ?? "Could not rasterise PDF for OCR.");
            }

            var builder = new System.Text.StringBuilder();
            var pageNumber = 0;
            foreach (var pageImage in render.OutputFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pageNumber++;
                var pageResult = await _engine.RecognizeImageAsync(pageImage, cancellationToken).ConfigureAwait(false);
                if (!pageResult.Success)
                {
                    return OcrDocumentResult.Failed(pageResult.ErrorMessage ?? $"OCR failed on page {pageNumber}.");
                }

                builder.AppendLine(pageResult.Text);
                builder.AppendLine();
            }

            var text = builder.ToString().TrimEnd();
            await File.WriteAllTextAsync(outputTextPath, text, cancellationToken).ConfigureAwait(false);
            return OcrDocumentResult.Ok(outputTextPath, text);
        }
        finally
        {
            TryDeleteDirectory(pageDirectory);
        }
    }

    /// <summary>
    /// Resolves the .txt output without overwriting an existing file. On a name collision it falls
    /// back to the batch planner's deterministic "<c>name__hash8</c>" convention, then to numbered
    /// variants, so repeated OCR runs never destroy earlier results.
    /// </summary>
    private static string ResolveTextOutputPath(string outputDirectory, string inputPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var candidate = Path.Combine(outputDirectory, $"{baseName}.txt");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var hash8 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(inputPath).ToUpperInvariant())))[..8];
        candidate = Path.Combine(outputDirectory, $"{baseName}__{hash8}.txt");
        var counter = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outputDirectory, $"{baseName}__{hash8}-{counter}.txt");
            counter++;
        }

        return candidate;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of temp page images.
        }
    }
}

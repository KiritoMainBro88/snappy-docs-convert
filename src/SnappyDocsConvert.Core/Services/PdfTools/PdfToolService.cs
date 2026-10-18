using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.PdfTools;

public sealed class PdfToolService : IPdfToolService
{
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly PageRangeParser _pageRangeParser;

    public PdfToolService()
        : this(new PageRangeParser())
    {
    }

    public PdfToolService(PageRangeParser pageRangeParser)
    {
        _pageRangeParser = pageRangeParser;
    }

    public Task<PdfToolResult> RunAsync(
        PdfToolRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = ValidateCommon(request);
            if (validation is not null)
            {
                return Task.FromResult(Failure(request.Operation, stopwatch.Elapsed, validation));
            }

            Directory.CreateDirectory(request.OutputDirectory);

            var outputFiles = request.Operation switch
            {
                PdfToolOperation.Merge => Merge(request, cancellationToken),
                PdfToolOperation.Split => Split(request, cancellationToken),
                PdfToolOperation.ExtractPages => ExtractPages(request, cancellationToken),
                PdfToolOperation.RotatePages => RotatePages(request, cancellationToken),
                PdfToolOperation.ImagesToPdf => ImagesToPdf(request, warnings, cancellationToken),
                PdfToolOperation.Compress => Compress(request, warnings, cancellationToken),
                PdfToolOperation.Watermark => Watermark(request, cancellationToken),
                PdfToolOperation.Encrypt => Encrypt(request, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported PDF tool operation: {request.Operation}")
            };

            foreach (var outputFile in outputFiles)
            {
                if (!File.Exists(outputFile) || new FileInfo(outputFile).Length <= 0)
                {
                    return Task.FromResult(Failure(
                        request.Operation,
                        stopwatch.Elapsed,
                        $"Output file was not produced: {outputFile}"));
                }
            }

            return Task.FromResult(new PdfToolResult
            {
                Success = true,
                Operation = request.Operation,
                OutputFiles = outputFiles,
                Duration = stopwatch.Elapsed,
                Warnings = warnings
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Failure(request.Operation, stopwatch.Elapsed, "PDF tool operation cancelled."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure(request.Operation, stopwatch.Elapsed, ex.Message));
        }
    }

    private IReadOnlyList<string> Merge(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 2);
        var outputPath = PlanOutputFile(request, "merged.pdf");

        using var outputDocument = new PdfDocument();
        foreach (var inputPath in request.InputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var inputDocument = OpenForTool(inputPath, PdfDocumentOpenMode.Import);
            foreach (var page in inputDocument.Pages)
            {
                outputDocument.AddPage(page);
            }
        }

        Save(outputDocument, outputPath);
        return new[] { outputPath };
    }

    private IReadOnlyList<string> Split(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        using var inputDocument = OpenForTool(request.InputPaths[0], PdfDocumentOpenMode.Import);
        var padding = Math.Max(3, inputDocument.PageCount.ToString().Length);
        var baseName = SafeFileName(Path.GetFileNameWithoutExtension(request.InputPaths[0]));
        var outputFiles = new List<string>();

        for (var index = 0; index < inputDocument.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageNumber = (index + 1).ToString($"D{padding}", CultureInfo.InvariantCulture);
            var fileName = $"{baseName}-page-{pageNumber}.pdf";
            var outputPath = PlanOutputFile(request.OutputDirectory, fileName, request.InputPaths);

            using var outputDocument = new PdfDocument();
            outputDocument.AddPage(inputDocument.Pages[index]);
            Save(outputDocument, outputPath);
            outputFiles.Add(outputPath);
        }

        return outputFiles;
    }

    private IReadOnlyList<string> ExtractPages(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        var outputPath = PlanOutputFile(request, "extracted.pdf");
        using var inputDocument = OpenForTool(request.InputPaths[0], PdfDocumentOpenMode.Import);
        var selection = _pageRangeParser.Parse(request.PageRanges, inputDocument.PageCount);
        if (!selection.Success)
        {
            throw new InvalidOperationException(selection.ErrorMessage);
        }

        using var outputDocument = new PdfDocument();
        foreach (var pageNumber in selection.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outputDocument.AddPage(inputDocument.Pages[pageNumber - 1]);
        }

        Save(outputDocument, outputPath);
        return new[] { outputPath };
    }

    private IReadOnlyList<string> RotatePages(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        var outputPath = PlanOutputFile(request, "rotated.pdf");
        using var inputDocument = OpenForTool(request.InputPaths[0], PdfDocumentOpenMode.Import);
        var selection = string.IsNullOrWhiteSpace(request.PageRanges)
            ? PageRangeSelection.Ok(Enumerable.Range(1, inputDocument.PageCount).ToArray())
            : _pageRangeParser.Parse(request.PageRanges, inputDocument.PageCount);
        if (!selection.Success)
        {
            throw new InvalidOperationException(selection.ErrorMessage);
        }

        var selectedPages = selection.Pages.ToHashSet();
        using var outputDocument = new PdfDocument();
        for (var index = 0; index < inputDocument.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = outputDocument.AddPage(inputDocument.Pages[index]);
            if (selectedPages.Contains(index + 1))
            {
                page.Rotate = NormalizeRotation(page.Rotate + (int)request.RotationAngle);
            }
        }

        Save(outputDocument, outputPath);
        return new[] { outputPath };
    }

    private IReadOnlyList<string> ImagesToPdf(
        PdfToolRequest request,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        ValidateImageInputs(request.InputPaths);
        var outputPath = PlanOutputFile(request, "images.pdf");
        using var outputDocument = new PdfDocument();
        var tempFiles = new List<string>();

        try
        {
            foreach (var inputPath in request.InputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imagePath = PrepareImagePath(inputPath, tempFiles, warnings);
                using var image = XImage.FromFile(imagePath);
                var page = outputDocument.AddPage();

                if (request.UseImageSizedPages)
                {
                    page.Width = XUnit.FromPoint(image.PointWidth);
                    page.Height = XUnit.FromPoint(image.PointHeight);
                }
                else
                {
                    page.Size = PageSize.A4;
                }

                using var gfx = XGraphics.FromPdfPage(page);
                var pageWidth = page.Width.Point;
                var pageHeight = page.Height.Point;
                var margin = request.UseImageSizedPages ? 0 : 36;
                var maxWidth = pageWidth - (margin * 2);
                var maxHeight = pageHeight - (margin * 2);
                var scale = Math.Min(maxWidth / image.PointWidth, maxHeight / image.PointHeight);
                scale = Math.Min(scale, 1d);
                var width = image.PointWidth * scale;
                var height = image.PointHeight * scale;
                var x = (pageWidth - width) / 2;
                var y = (pageHeight - height) / 2;
                gfx.DrawImage(image, x, y, width, height);
            }

            Save(outputDocument, outputPath);
            return new[] { outputPath };
        }
        finally
        {
            foreach (var tempFile in tempFiles)
            {
                TryDelete(tempFile);
            }
        }
    }

    private IReadOnlyList<string> Encrypt(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        if (string.IsNullOrEmpty(request.UserPassword))
        {
            throw new InvalidOperationException("A user password is required to encrypt the PDF.");
        }

        var outputPath = PlanOutputFile(request, "encrypted.pdf");
        using var document = OpenForTool(request.InputPaths[0], PdfDocumentOpenMode.Modify);

        cancellationToken.ThrowIfCancellationRequested();
        var security = document.SecuritySettings;
        security.UserPassword = request.UserPassword;
        security.OwnerPassword = string.IsNullOrEmpty(request.OwnerPassword)
            ? request.UserPassword
            : request.OwnerPassword;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
        return new[] { outputPath };
    }

    private IReadOnlyList<string> Watermark(PdfToolRequest request, CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        if (string.IsNullOrWhiteSpace(request.WatermarkText))
        {
            throw new InvalidOperationException("Watermark text is required.");
        }

        var outputPath = PlanOutputFile(request, "watermarked.pdf");
        var opacity = Math.Clamp(request.WatermarkOpacity, 0.02d, 1d);
        SystemFontResolver.EnsureRegistered();
        using var document = OpenForTool(request.InputPaths[0], PdfDocumentOpenMode.Modify);

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var width = page.Width.Point;
            var height = page.Height.Point;
            // Scale the font to the page so the watermark spans the diagonal regardless of page size.
            var fontSize = Math.Max(18d, Math.Min(width, height) / 10d);
            var font = new XFont("Arial", fontSize, XFontStyleEx.Bold);
            var color = XColor.FromArgb((int)Math.Round(opacity * 255), 128, 128, 128);
            var brush = new XSolidBrush(color);

            // Draw centered, rotated 45° to mimic a classic diagonal watermark.
            gfx.TranslateTransform(width / 2, height / 2);
            gfx.RotateTransform(-45);
            var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString(request.WatermarkText, font, brush, new XPoint(0, 0), format);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
        return new[] { outputPath };
    }

    private IReadOnlyList<string> Compress(
        PdfToolRequest request,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        ValidatePdfInputs(request.InputPaths, minCount: 1, maxCount: 1);
        var inputPath = request.InputPaths[0];
        var outputPath = PlanOutputFile(request, "compressed.pdf");

        var originalLength = new FileInfo(inputPath).Length;

        using (var inputDocument = OpenForTool(inputPath, PdfDocumentOpenMode.Import))
        using (var outputDocument = new PdfDocument())
        {
            // Lossless re-save: import every page and apply PdfSharp's best content-stream
            // compression + object-stream packing. No rasterization, so text stays selectable and
            // quality is unchanged. Image-downsampling (lossy) is a separate, opt-in follow-up.
            outputDocument.Options.CompressContentStreams = true;
            outputDocument.Options.NoCompression = false;
            outputDocument.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;

            foreach (var page in inputDocument.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                outputDocument.AddPage(page);
            }

            Save(outputDocument, outputPath);
        }

        var newLength = new FileInfo(outputPath).Length;
        if (newLength >= originalLength)
        {
            // Already optimized: report it instead of pretending we shrank it.
            var deltaPercent = originalLength == 0
                ? 0
                : (double)(newLength - originalLength) / originalLength * 100d;
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "PDF was already optimized; size changed by {0:+0.0;-0.0}% ({1} -> {2} bytes).",
                deltaPercent,
                originalLength,
                newLength));
        }
        else
        {
            var savedPercent = (double)(originalLength - newLength) / originalLength * 100d;
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Compressed PDF by {0:0.0}% ({1} -> {2} bytes).",
                savedPercent,
                originalLength,
                newLength));
        }

        return new[] { outputPath };
    }

    private static string? ValidateCommon(PdfToolRequest request)
    {
        if (request.InputPaths.Count == 0)
        {
            return "At least one input file is required.";
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return "Output directory is required.";
        }

        var fullOutputDirectory = Path.GetFullPath(request.OutputDirectory);
        if (File.Exists(fullOutputDirectory))
        {
            return "Output directory points to a file.";
        }

        foreach (var inputPath in request.InputPaths)
        {
            if (!File.Exists(inputPath))
            {
                return $"Input file does not exist: {inputPath}";
            }
        }

        return null;
    }

    private static void ValidatePdfInputs(IReadOnlyList<string> inputPaths, int minCount, int? maxCount = null)
    {
        if (inputPaths.Count < minCount)
        {
            throw new InvalidOperationException($"Expected at least {minCount} PDF input file(s).");
        }

        if (maxCount is not null && inputPaths.Count > maxCount.Value)
        {
            throw new InvalidOperationException($"Expected no more than {maxCount.Value} PDF input file(s).");
        }

        foreach (var inputPath in inputPaths)
        {
            if (!PdfExtensions.Contains(Path.GetExtension(inputPath)))
            {
                throw new InvalidOperationException($"PDF operation requires .pdf input: {inputPath}");
            }
        }
    }

    private static void ValidateImageInputs(IReadOnlyList<string> inputPaths)
    {
        foreach (var inputPath in inputPaths)
        {
            if (!ImageExtensions.Contains(Path.GetExtension(inputPath)))
            {
                throw new InvalidOperationException($"Images to PDF requires PNG, JPEG, or WebP input: {inputPath}");
            }
        }
    }

    private static string PlanOutputFile(PdfToolRequest request, string defaultFileName)
        => PlanOutputFile(request.OutputDirectory, request.OutputFileName ?? defaultFileName, request.InputPaths);

    private static string PlanOutputFile(
        string outputDirectory,
        string fileName,
        IReadOnlyList<string> sourcePaths)
    {
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        if (fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output file name must not contain path separators.");
        }

        var safeFileName = SafeFileName(fileName);
        if (!safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            safeFileName += ".pdf";
        }

        var outputPath = Path.GetFullPath(Path.Combine(fullOutputDirectory, safeFileName));
        if (!outputPath.StartsWith(fullOutputDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Output path escapes output directory.");
        }

        foreach (var sourcePath in sourcePaths)
        {
            if (string.Equals(
                Path.GetFullPath(sourcePath),
                outputPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Output path must not overwrite a source file.");
            }
        }

        // Collision avoidance: never silently overwrite an existing file. Fall back to the
        // pipeline's deterministic "<name>__<hash8>" convention, then to numbered suffixes.
        if (File.Exists(outputPath))
        {
            var stem = Path.GetFileNameWithoutExtension(safeFileName);
            var hash8 = ComputeSourceHash8(sourcePaths);
            var candidate = Path.Combine(fullOutputDirectory, $"{stem}__{hash8}.pdf");
            var counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(fullOutputDirectory, $"{stem}__{hash8}-{counter}.pdf");
                counter++;
            }

            outputPath = candidate;
        }

        return outputPath;
    }

    /// <summary>Stable 8-hex-character digest of the source path set (mirrors the batch planner naming).</summary>
    private static string ComputeSourceHash8(IReadOnlyList<string> sourcePaths)
    {
        var material = string.Join(
            "|",
            sourcePaths.Select(static path => Path.GetFullPath(path).ToUpperInvariant()));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(digest)[..8];
    }

    private static string SafeFileName(string value)
    {
        var fileName = Path.GetFileName(value);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            fileName = "output.pdf";
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '-');
        }

        return fileName;
    }

    private static string PrepareImagePath(
        string inputPath,
        List<string> tempFiles,
        List<string> warnings)
    {
        if (!Path.GetExtension(inputPath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap is null)
        {
            throw new InvalidOperationException($"Could not decode WebP image: {inputPath}");
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new InvalidOperationException($"Could not convert WebP image: {inputPath}");
        }

        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"SnappyDocsConvert-{Guid.NewGuid():N}.png");
        using (var stream = File.Create(tempFile))
        {
            data.SaveTo(stream);
        }

        tempFiles.Add(tempFile);
        warnings.Add("Converted WebP input to temporary PNG for PDF embedding.");
        return tempFile;
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = rotation % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    /// <summary>
    /// Opens a PDF for a tool operation, translating PDFsharp's encrypted-file errors into a clear,
    /// actionable message. The message never echoes the password (it is never known here).
    /// </summary>
    private static PdfDocument OpenForTool(string inputPath, PdfDocumentOpenMode mode)
    {
        try
        {
            return PdfReader.Open(inputPath, mode);
        }
        catch (Exception ex) when (
            ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "This PDF is password-protected. Remove the password protection first (or open it with its password in a PDF reader and re-save an unprotected copy), then run the tool again.",
                ex);
        }
    }

    private static void Save(PdfDocument document, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
    }

    private static PdfToolResult Failure(PdfToolOperation operation, TimeSpan duration, string? errorMessage)
        => new()
        {
            Success = false,
            Operation = operation,
            Duration = duration,
            ErrorMessage = errorMessage ?? "PDF tool operation failed."
        };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort temp cleanup only.
        }
    }
}

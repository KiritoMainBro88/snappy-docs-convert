using System.Security.Cryptography;
using System.Text;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public sealed class BatchOutputPlanner : IBatchOutputPlanner
{
    public BatchOutputPlan Plan(BatchConversionJob job)
        => new(job.Items.Select(item => PlanItem(item, job.Options)).ToArray());

    public BatchOutputItemPlan PlanItem(
        BatchConversionItem item,
        BatchConversionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputRoot))
        {
            throw new InvalidOperationException("Batch output root is required.");
        }

        var inputPath = Path.GetFullPath(item.InputPath);
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var safeBaseName = SanitizeFileName(Path.GetFileNameWithoutExtension(inputPath));
        var hash8 = Hash8(inputPath);
        var folderName = $"{safeBaseName}__{hash8}";

        var pdfOutputPath = Path.GetFullPath(
            Path.Combine(outputRoot, "pdf", $"{folderName}.pdf"));
        var imageOutputDirectory = Path.GetFullPath(
            Path.Combine(outputRoot, "images", folderName));

        EnsureInsideRoot(pdfOutputPath, outputRoot);
        EnsureInsideRoot(imageOutputDirectory, outputRoot);

        return new BatchOutputItemPlan
        {
            InputPath = inputPath,
            OutputRoot = outputRoot,
            SafeBaseName = safeBaseName,
            Hash8 = hash8,
            PdfOutputPath = pdfOutputPath,
            ImageOutputDirectory = imageOutputDirectory,
            PagePrefix = PagePrefixFor(inputPath)
        };
    }

    private static string PagePrefixFor(string inputPath)
        => Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".ppt" or ".pptx" or ".odp" => "slide",
            _ => "page"
        };

    private static string SanitizeFileName(string? fileName)
    {
        var value = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray())
            .Trim(' ', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }

    private static string Hash8(string inputPath)
    {
        var normalized = Path.GetFullPath(inputPath).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private static void EnsureInsideRoot(string path, string outputRoot)
    {
        var fullRoot = Path.GetFullPath(outputRoot);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Batch output path escaped the output root.");
        }
    }
}

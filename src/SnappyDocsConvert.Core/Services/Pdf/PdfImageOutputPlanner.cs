using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Pdf;

internal sealed class PdfImageOutputPlanner
{
    public string GetExtension(ImageOutputFormat format)
        => format switch
        {
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Jpeg => ".jpg",
            ImageOutputFormat.Webp => ".webp",
            _ => ".png"
        };

    public string SanitizePrefix(string? prefix)
    {
        var value = string.IsNullOrWhiteSpace(prefix) ? "page" : prefix.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray())
            .Trim(' ', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "page" : sanitized;
    }

    public string PlanOutputPath(
        string outputDirectory,
        string pagePrefix,
        int pageNumber,
        int pageCount,
        ImageOutputFormat format,
        OverwritePolicy overwritePolicy)
    {
        var padding = Math.Max(3, pageCount.ToString().Length);
        var extension = GetExtension(format);
        var fileName = $"{pagePrefix}-{pageNumber.ToString().PadLeft(padding, '0')}{extension}";
        var plannedPath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        EnsureInsideDirectory(plannedPath, outputDirectory);

        if (overwritePolicy is not OverwritePolicy.AutoRename || !File.Exists(plannedPath))
        {
            return plannedPath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        for (var index = 1; index < 10_000; index++)
        {
            var candidateName = $"{baseName} ({index}){extension}";
            var candidatePath = Path.GetFullPath(Path.Combine(outputDirectory, candidateName));
            EnsureInsideDirectory(candidatePath, outputDirectory);

            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not create unique output image name for {fileName}.");
    }

    private static void EnsureInsideDirectory(string filePath, string directoryPath)
    {
        var fullDirectory = Path.GetFullPath(directoryPath);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDirectory += Path.DirectorySeparatorChar;
        }

        if (!filePath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Planned image output path escaped the output directory.");
        }
    }
}

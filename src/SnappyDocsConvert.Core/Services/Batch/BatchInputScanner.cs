using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public sealed class BatchInputScanner : IBatchInputScanner
{
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".rtf",
            ".odt",
            ".ppt",
            ".pptx",
            ".odp"
        };

    public BatchInputScanResult Scan(IEnumerable<string> paths)
    {
        var accepted = new List<string>();
        var rejected = new List<string>();

        foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                AddFile(path, accepted, rejected);
                continue;
            }

            if (Directory.Exists(path))
            {
                foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    AddFile(filePath, accepted, rejected);
                }

                continue;
            }

            rejected.Add(path);
        }

        return new BatchInputScanResult(
            accepted.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            rejected.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void AddFile(
        string filePath,
        List<string> accepted,
        List<string> rejected)
    {
        if (SupportedExtensions.Contains(Path.GetExtension(filePath)))
        {
            accepted.Add(filePath);
        }
        else
        {
            rejected.Add(filePath);
        }
    }
}

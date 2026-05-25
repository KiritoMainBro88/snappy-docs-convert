using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.LibreOffice;

public sealed class LibreOfficeCommandBuilder
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
        ".rtf",
        ".odt",
        ".ppt",
        ".pptx",
        ".odp"
    };

    public bool IsSupportedInput(string inputPath)
        => SupportedExtensions.Contains(Path.GetExtension(inputPath));

    public ConversionFileKind GetFileKind(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);
        return extension.ToLowerInvariant() switch
        {
            ".doc" or ".docx" or ".rtf" or ".odt" => ConversionFileKind.WordDocument,
            ".ppt" or ".pptx" or ".odp" => ConversionFileKind.Presentation,
            ".pdf" => ConversionFileKind.Pdf,
            _ => ConversionFileKind.Unsupported
        };
    }

    public string GetExpectedPdfPath(string inputPath, string outputDirectory)
        => Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

    public LibreOfficeCommand BuildConvertToPdfCommand(
        string executablePath,
        string inputPath,
        string outputDirectory,
        string tempProfileDirectory)
    {
        var profileUri = new Uri(tempProfileDirectory).AbsoluteUri;
        var arguments = new[]
        {
            "--headless",
            "--nologo",
            "--nodefault",
            "--nofirststartwizard",
            "--norestore",
            $"-env:UserInstallation={profileUri}",
            "--convert-to",
            "pdf",
            "--outdir",
            outputDirectory,
            inputPath
        };

        return new LibreOfficeCommand(
            executablePath,
            arguments,
            tempProfileDirectory,
            GetExpectedPdfPath(inputPath, outputDirectory));
    }
}

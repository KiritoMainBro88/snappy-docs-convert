namespace SnappyDocsConvert.Core.Models;

public sealed record PdfRenderRequest(
    string InputPdfPath,
    string OutputDirectory)
{
    public PdfRenderOptions Options { get; init; } = new();
}

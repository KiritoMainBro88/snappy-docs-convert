namespace SnappyDocsConvert.Core.Models;

public sealed record PdfToolRequest
{
    public PdfToolOperation Operation { get; init; }

    public IReadOnlyList<string> InputPaths { get; init; } = Array.Empty<string>();

    public string OutputDirectory { get; init; } = "";

    public string? OutputFileName { get; init; }

    public string? PageRanges { get; init; }

    public PdfRotationAngle RotationAngle { get; init; } = PdfRotationAngle.Degrees90;

    public bool UseImageSizedPages { get; init; }
}

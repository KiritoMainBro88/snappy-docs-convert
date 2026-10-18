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

    /// <summary>Watermark text drawn diagonally on every page. Required for the Watermark operation.</summary>
    public string? WatermarkText { get; init; }

    /// <summary>Watermark opacity 0..1 (default 0.15). Clamped at runtime.</summary>
    public double WatermarkOpacity { get; init; } = 0.15;

    /// <summary>User password required to open the PDF. Required for the Encrypt operation.</summary>
    public string? UserPassword { get; init; }

    /// <summary>Optional owner password (permissions). Defaults to the user password when omitted.</summary>
    public string? OwnerPassword { get; init; }
}

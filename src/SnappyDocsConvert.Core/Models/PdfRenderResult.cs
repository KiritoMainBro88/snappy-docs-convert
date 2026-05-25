namespace SnappyDocsConvert.Core.Models;

public sealed record PdfRenderResult
{
    public required bool Success { get; init; }

    public required string InputPdfPath { get; init; }

    public required string OutputDirectory { get; init; }

    public int PagesRendered { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<PdfPageRenderResult> PageResults { get; init; } = Array.Empty<PdfPageRenderResult>();

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

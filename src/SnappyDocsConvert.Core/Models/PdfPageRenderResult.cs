namespace SnappyDocsConvert.Core.Models;

public sealed record PdfPageRenderResult
{
    public required int PageNumber { get; init; }

    public required string OutputPath { get; init; }

    public bool Skipped { get; init; }

    public string? Warning { get; init; }
}

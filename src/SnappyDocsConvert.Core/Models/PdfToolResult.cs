namespace SnappyDocsConvert.Core.Models;

public sealed record PdfToolResult
{
    public bool Success { get; init; }

    public PdfToolOperation Operation { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = Array.Empty<string>();

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

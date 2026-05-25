namespace SnappyDocsConvert.Core.Models;

public sealed record ConversionResult
{
    public required bool Success { get; init; }

    public required ConversionStatus Status { get; init; }

    public required string InputPath { get; init; }

    public string? OutputPdfPath { get; init; }

    public required ConversionEngineKind EngineKind { get; init; }

    public TimeSpan Duration { get; init; }

    public int? ExitCode { get; init; }

    public string? StdoutSnippet { get; init; }

    public string? StderrSnippet { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

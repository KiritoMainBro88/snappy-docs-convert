namespace SnappyDocsConvert.Core.Models;

public sealed record BatchConversionItemResult
{
    public required string InputPath { get; init; }

    public required BatchConversionItemStatus Status { get; init; }

    public ConversionEngineKind? DocumentEngine { get; init; }

    public string? OutputPdfPath { get; init; }

    public string? ImageOutputDirectory { get; init; }

    public IReadOnlyList<string> ImageFiles { get; init; } = Array.Empty<string>();

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public BatchOutputItemPlan? OutputPlan { get; init; }

    public TimeSpan Duration { get; init; }
}

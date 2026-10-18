namespace SnappyDocsConvert.Core.Models;

public sealed record BatchFallbackInfo
{
    public required ConversionEngineKind PrimaryEngine { get; init; }

    public required string PrimaryFailureReason { get; init; }

    public required ConversionEngineKind FallbackEngine { get; init; }

    public required bool FallbackSucceeded { get; init; }
}

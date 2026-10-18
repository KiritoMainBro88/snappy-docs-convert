namespace SnappyDocsConvert.Core.Models;

public sealed record BatchEngineEligibilityResult
{
    // Ordered, de-duplicated; empty when no engine is needed (.pdf) or none is eligible.
    public IReadOnlyList<ConversionEngineKind> EligibleEngines { get; init; }
        = Array.Empty<ConversionEngineKind>();

    public static BatchEngineEligibilityResult Empty { get; } = new();

    public static BatchEngineEligibilityResult From(IEnumerable<ConversionEngineKind> engines)
        => new() { EligibleEngines = engines.Distinct().ToArray() };
}

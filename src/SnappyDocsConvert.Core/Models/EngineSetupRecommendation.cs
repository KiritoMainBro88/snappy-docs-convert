namespace SnappyDocsConvert.Core.Models;

public sealed record EngineSetupRecommendation(
    EngineSetupRecommendationSeverity Severity,
    string Title,
    string Message,
    IReadOnlyList<EngineSetupAction> Actions);

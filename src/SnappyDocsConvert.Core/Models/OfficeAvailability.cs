namespace SnappyDocsConvert.Core.Models;

public sealed record OfficeAvailability(
    bool WordAvailable,
    bool PowerPointAvailable,
    bool CanConvertWordDocuments,
    bool CanConvertPowerPointPresentations,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<EngineSetupRecommendation> Recommendations,
    string? VersionSummary);

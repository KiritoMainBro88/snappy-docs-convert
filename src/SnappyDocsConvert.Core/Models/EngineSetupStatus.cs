namespace SnappyDocsConvert.Core.Models;

public sealed record EngineSetupStatus(
    bool MicrosoftOfficeAvailable,
    bool LibreOfficeAvailable,
    ConversionEngineKind? PreferredEngine,
    bool CanConvertOfficeDocuments,
    string? BlockingReason,
    IReadOnlyList<EngineSetupRecommendation> Recommendations);

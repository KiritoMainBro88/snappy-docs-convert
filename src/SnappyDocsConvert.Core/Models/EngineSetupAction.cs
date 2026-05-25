namespace SnappyDocsConvert.Core.Models;

public sealed record EngineSetupAction(
    string Label,
    EngineSetupActionKind Kind,
    string? Target);

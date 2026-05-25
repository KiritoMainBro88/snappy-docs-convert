namespace SnappyDocsConvert.Core.Models;

public sealed record BatchEngineSelectionResult
{
    public required bool Success { get; init; }

    public ConversionEngineKind? EngineKind { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    public static BatchEngineSelectionResult NoneNeeded()
        => new() { Success = true };

    public static BatchEngineSelectionResult Selected(ConversionEngineKind engineKind)
        => new() { Success = true, EngineKind = engineKind };

    public static BatchEngineSelectionResult Failed(
        string errorMessage,
        IReadOnlyList<string>? recommendations = null)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            Recommendations = recommendations ?? Array.Empty<string>()
        };
}

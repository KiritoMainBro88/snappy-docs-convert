namespace SnappyDocsConvert.Core.Models.Updates;

public sealed record AppUpdateCheckResult
{
    public bool Success { get; init; }

    public bool UpdateAvailable { get; init; }

    public string CurrentVersion { get; init; } = "";

    public string? LatestVersion { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public string? ReleasePageUrl { get; init; }

    public string? ReleaseNotes { get; init; }

    public AppUpdateAsset? PreferredAsset { get; init; }

    public IReadOnlyList<AppUpdateAsset> Assets { get; init; } = Array.Empty<AppUpdateAsset>();

    public string? ErrorMessage { get; init; }

    public static AppUpdateCheckResult Failed(string currentVersion, string message)
        => new()
        {
            Success = false,
            CurrentVersion = currentVersion,
            ErrorMessage = message
        };
}

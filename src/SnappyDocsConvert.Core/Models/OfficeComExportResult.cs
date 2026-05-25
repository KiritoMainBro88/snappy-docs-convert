namespace SnappyDocsConvert.Core.Models;

public sealed record OfficeComExportResult(
    bool Success,
    bool TimedOut,
    TimeSpan Duration,
    string? ErrorMessage,
    IReadOnlyList<string> Warnings)
{
    public static OfficeComExportResult Succeeded(TimeSpan duration)
        => new(true, false, duration, null, Array.Empty<string>());

    public static OfficeComExportResult Failed(string errorMessage, TimeSpan duration)
        => new(false, false, duration, errorMessage, Array.Empty<string>());

    public static OfficeComExportResult Timeout(TimeSpan duration)
        => new(false, true, duration, "Microsoft Office COM export timed out.", Array.Empty<string>());
}

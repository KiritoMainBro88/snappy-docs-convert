namespace SnappyDocsConvert.Core.Models;

public sealed record EngineAvailability(
    bool IsAvailable,
    string? ExecutablePath,
    string? Version,
    string? Reason)
{
    public static EngineAvailability Available(string executablePath, string? version = null)
        => new(true, executablePath, version, null);

    public static EngineAvailability Unavailable(string reason)
        => new(false, null, null, reason);
}

namespace SnappyDocsConvert.Core.Models;

public sealed record LibreOfficeOptions
{
    public string? ExecutablePath { get; init; }

    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(120);

    public TimeSpan VersionProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public bool ProbeVersion { get; init; } = true;
}

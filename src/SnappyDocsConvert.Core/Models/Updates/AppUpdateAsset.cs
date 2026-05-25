namespace SnappyDocsConvert.Core.Models.Updates;

public sealed record AppUpdateAsset(
    string Name,
    string BrowserDownloadUrl,
    long SizeBytes,
    string? Sha256Digest)
{
    public bool IsInstaller => Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public bool IsPortableZip => Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

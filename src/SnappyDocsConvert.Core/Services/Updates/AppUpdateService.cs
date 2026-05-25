using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnappyDocsConvert.Core.Models.Updates;

namespace SnappyDocsConvert.Core.Services.Updates;

public sealed class AppUpdateService
{
    private const string Owner = "KiritoMainBro88";
    private const string Repository = "snappy-docs-convert";
    private readonly HttpClient _httpClient;

    public AppUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("kmb-file-tools", "0.1"));
        }
    }

    public async Task<AppUpdateCheckResult> CheckAsync(
        string currentVersion,
        UpdateChannel channel,
        CancellationToken cancellationToken)
    {
        try
        {
            var releases = await LoadCandidateReleasesAsync(channel, cancellationToken).ConfigureAwait(false);
            var latest = releases
                .Where(release => !release.Draft)
                .Where(release => channel == UpdateChannel.Prerelease || !release.Prerelease)
                .OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault(release => ReleaseVersionComparer.Compare(release.TagName, currentVersion) > 0);

            if (latest is null)
            {
                return new AppUpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion
                };
            }

            var assets = latest.Assets
                .Select(asset => new AppUpdateAsset(
                    asset.Name,
                    asset.BrowserDownloadUrl,
                    asset.Size,
                    NormalizeDigest(asset.Digest)))
                .ToArray();

            return new AppUpdateCheckResult
            {
                Success = true,
                UpdateAvailable = true,
                CurrentVersion = currentVersion,
                LatestVersion = latest.TagName,
                PublishedAt = latest.PublishedAt,
                ReleasePageUrl = latest.HtmlUrl,
                ReleaseNotes = latest.Body,
                Assets = assets,
                PreferredAsset = assets.FirstOrDefault(asset => asset.IsInstaller)
                    ?? assets.FirstOrDefault(asset => asset.IsPortableZip)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return AppUpdateCheckResult.Failed(currentVersion, $"Update check failed: {ex.Message}");
        }
    }

    public async Task<string> DownloadAsync(
        AppUpdateAsset asset,
        string targetDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);
        var outputPath = Path.Combine(targetDirectory, SanitizeFileName(asset.Name));

        using var response = await _httpClient.GetAsync(
            asset.BrowserDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        {
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(outputPath);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                totalRead += read;
                if (asset.SizeBytes > 0)
                {
                    progress?.Report(Math.Clamp((double)totalRead / asset.SizeBytes * 100d, 0d, 100d));
                }
            }
        }

        progress?.Report(100d);

        if (!string.IsNullOrWhiteSpace(asset.Sha256Digest) &&
            !VerifySha256(outputPath, asset.Sha256Digest))
        {
            File.Delete(outputPath);
            throw new InvalidOperationException("Downloaded update checksum does not match the release asset digest.");
        }

        return outputPath;
    }

    public static bool VerifySha256(string filePath, string expectedSha256)
    {
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<GitHubReleaseDto>> LoadCandidateReleasesAsync(
        UpdateChannel channel,
        CancellationToken cancellationToken)
    {
        var url = channel == UpdateChannel.Stable
            ? $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest"
            : $"https://api.github.com/repos/{Owner}/{Repository}/releases";

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        if (channel == UpdateChannel.Stable)
        {
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, options, cancellationToken)
                .ConfigureAwait(false);
            return release is null ? Array.Empty<GitHubReleaseDto>() : new[] { release };
        }

        return await JsonSerializer.DeserializeAsync<GitHubReleaseDto[]>(stream, options, cancellationToken)
                .ConfigureAwait(false)
            ?? Array.Empty<GitHubReleaseDto>();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        const string prefix = "sha256:";
        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digest[prefix.Length..]
            : digest;
    }

    private sealed record GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public GitHubAssetDto[] Assets { get; init; } = Array.Empty<GitHubAssetDto>();
    }

    private sealed record GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = "";

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }
}

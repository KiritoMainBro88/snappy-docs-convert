using System.Net;
using System.Text;
using SnappyDocsConvert.Core.Models.Updates;
using SnappyDocsConvert.Core.Services.Updates;

namespace SnappyDocsConvert.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public void ReleaseVersionComparer_detects_newer_prerelease()
    {
        Assert.True(ReleaseVersionComparer.IsNewer("v0.1.0-beta.2", "0.1.0-beta.1"));
    }

    [Fact]
    public void ReleaseVersionComparer_treats_stable_as_newer_than_same_prerelease()
    {
        Assert.True(ReleaseVersionComparer.IsNewer("v0.1.0", "v0.1.0-beta.2"));
    }

    [Fact]
    public async Task Stable_channel_ignores_prerelease_list_and_uses_latest_endpoint()
    {
        var service = new AppUpdateService(new HttpClient(new FakeHandler(request =>
        {
            Assert.EndsWith("/releases/latest", request.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
            return Json("""
                {
                  "tag_name": "v0.1.0",
                  "html_url": "https://example.test/release",
                  "draft": false,
                  "prerelease": false,
                  "published_at": "2026-01-01T00:00:00Z",
                  "assets": []
                }
                """);
        })));

        var result = await service.CheckAsync("v0.1.0-beta.2", UpdateChannel.Stable, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.UpdateAvailable);
        Assert.Equal("v0.1.0", result.LatestVersion);
    }

    [Fact]
    public async Task Prerelease_channel_finds_beta_release_and_prefers_installer()
    {
        var service = new AppUpdateService(new HttpClient(new FakeHandler(_ => Json("""
            [
              {
                "tag_name": "v0.1.0-beta.2",
                "html_url": "https://example.test/release",
                "draft": false,
                "prerelease": true,
                "published_at": "2026-01-01T00:00:00Z",
                "assets": [
                  {
                    "name": "kmb-file-tools-portable-win-x64-v0.1.0-beta.2.zip",
                    "browser_download_url": "https://example.test/app.zip",
                    "size": 12,
                    "digest": "sha256:ABC"
                  },
                  {
                    "name": "kmb-file-tools-setup-win-x64-v0.1.0-beta.2.exe",
                    "browser_download_url": "https://example.test/app.exe",
                    "size": 20,
                    "digest": "sha256:DEF"
                  }
                ]
              }
            ]
            """))));

        var result = await service.CheckAsync("v0.1.0-beta.1", UpdateChannel.Prerelease, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.UpdateAvailable);
        Assert.Equal("v0.1.0-beta.2", result.LatestVersion);
        Assert.NotNull(result.PreferredAsset);
        Assert.True(result.PreferredAsset!.IsInstaller);
    }

    [Fact]
    public async Task No_newer_version_returns_no_update()
    {
        var service = new AppUpdateService(new HttpClient(new FakeHandler(_ => Json("""
            [
              {
                "tag_name": "v0.1.0-beta.1",
                "html_url": "https://example.test/release",
                "draft": false,
                "prerelease": true,
                "published_at": "2026-01-01T00:00:00Z",
                "assets": []
              }
            ]
            """))));

        var result = await service.CheckAsync("v0.1.0-beta.2", UpdateChannel.Prerelease, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task Network_failure_returns_structured_failure()
    {
        var service = new AppUpdateService(new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline"))));

        var result = await service.CheckAsync("v0.1.0-beta.2", UpdateChannel.Prerelease, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("offline", result.ErrorMessage);
    }

    [Fact]
    public async Task Download_reports_progress_and_blocks_checksum_mismatch()
    {
        var bytes = Encoding.UTF8.GetBytes("payload");
        var service = new AppUpdateService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        })));
        var asset = new AppUpdateAsset("update.exe", "https://example.test/update.exe", bytes.Length, "BAD");
        var progressValues = new List<double>();
        var outputDir = Path.Combine(Path.GetTempPath(), "kmb-file-tools-test-" + Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DownloadAsync(asset, outputDir, new Progress<double>(progressValues.Add), CancellationToken.None));
        Assert.Contains(progressValues, value => value >= 100d);
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}

using SnappyDocsConvert.App.Localization;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Example-based unit tests for <see cref="LocalizedStrings.Get"/> on the new fallback keys.
/// Validates: Requirements 8.2, 8.3.
///
/// These complement the property test in <see cref="LocalizedFallbackStringsTests"/> (which asserts
/// resolution is non-empty/distinct and that runtime values are preserved) by pinning the exact
/// English and Vietnamese strings for each new key:
///   - EnableEngineFallback (the setting label),
///   - FallbackAttempt, FallbackSuccess, BothEnginesFailed (the activity-log templates).
///
/// CPU-only, no real Office/LibreOffice, no new dependency (per AGENTS.md).
/// </summary>
public sealed class LocalizedFallbackStringsExampleTests
{
    [Theory]
    [InlineData("EnableEngineFallback", "Auto fallback to the other engine on failure")]
    [InlineData("FallbackAttempt", "{0}: {1} failed ({2}); retrying with {3}.")]
    [InlineData("FallbackSuccess", "{0}: {1} produced the PDF.")]
    [InlineData("BothEnginesFailed", "{0}: both engines failed.")]
    public void Get_English_ReturnsExpectedString(string key, string expected)
    {
        Assert.Equal(expected, LocalizedStrings.Get(Language.English, key));
    }

    [Theory]
    [InlineData("EnableEngineFallback", "Tự động chuyển sang bộ chuyển đổi khác khi lỗi")]
    [InlineData("FallbackAttempt", "{0}: {1} thất bại ({2}); đang thử lại với {3}.")]
    [InlineData("FallbackSuccess", "{0}: {1} đã tạo PDF.")]
    [InlineData("BothEnginesFailed", "{0}: cả hai bộ chuyển đổi đều thất bại.")]
    public void Get_Vietnamese_ReturnsExpectedString(string key, string expected)
    {
        Assert.Equal(expected, LocalizedStrings.Get(Language.Vietnamese, key));
    }
}

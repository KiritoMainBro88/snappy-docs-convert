using SnappyDocsConvert.Core.Models.Updates;

namespace SnappyDocsConvert.App.Services;

public sealed class AppSettingsData
{
    public AppLanguagePreference Language { get; set; } = AppLanguagePreference.System;

    public AppThemePreference Theme { get; set; } = AppThemePreference.System;

    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Prerelease;

    public bool EnableEngineFallback { get; set; } = true;

    /// <summary>Optional path to a locally-installed PaddleOCR executable. Null/empty = OCR disabled.</summary>
    public string? PaddleOcrPath { get; set; }
}

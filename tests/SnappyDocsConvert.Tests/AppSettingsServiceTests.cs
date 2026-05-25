using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.Core.Models.Updates;

namespace SnappyDocsConvert.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void Missing_settings_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "kmb-file-tools-settings-" + Guid.NewGuid(), "settings.json");
        var service = new AppSettingsService(path);

        var settings = service.Load();

        Assert.Equal(AppLanguagePreference.System, settings.Language);
        Assert.Equal(AppThemePreference.System, settings.Theme);
        Assert.Equal(UpdateChannel.Prerelease, settings.UpdateChannel);
    }

    [Fact]
    public void Corrupt_settings_file_returns_defaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kmb-file-tools-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "{ not json");
        var service = new AppSettingsService(path);

        var settings = service.Load();

        Assert.Equal(AppLanguagePreference.System, settings.Language);
        Assert.Equal(AppThemePreference.System, settings.Theme);
        Assert.Equal(UpdateChannel.Prerelease, settings.UpdateChannel);
    }

    [Fact]
    public void Invalid_enum_values_are_normalized()
    {
        var settings = new AppSettingsData
        {
            Language = (AppLanguagePreference)999,
            Theme = (AppThemePreference)999,
            UpdateChannel = (UpdateChannel)999
        };

        var normalized = AppSettingsService.Normalize(settings);

        Assert.Equal(AppLanguagePreference.System, normalized.Language);
        Assert.Equal(AppThemePreference.System, normalized.Theme);
        Assert.Equal(UpdateChannel.Prerelease, normalized.UpdateChannel);
    }
}

using System.Globalization;
using System.IO;
using System.Text.Json;
using SnappyDocsConvert.App.Localization;
using SnappyDocsConvert.Core.Models.Updates;

namespace SnappyDocsConvert.App.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "kmb-file-tools",
            "settings.json"))
    {
    }

    public AppSettingsService(string settingsPath)
    {
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public AppSettingsData Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettingsData();
            }

            var json = File.ReadAllText(SettingsPath);
            return Normalize(JsonSerializer.Deserialize<AppSettingsData>(json, JsonOptions) ?? new AppSettingsData());
        }
        catch
        {
            return new AppSettingsData();
        }
    }

    public static AppSettingsData Normalize(AppSettingsData settings)
    {
        if (!Enum.IsDefined(settings.Language))
        {
            settings.Language = AppLanguagePreference.System;
        }

        if (!Enum.IsDefined(settings.Theme))
        {
            settings.Theme = AppThemePreference.System;
        }

        if (!Enum.IsDefined(settings.UpdateChannel))
        {
            settings.UpdateChannel = DefaultUpdateChannel;
        }

        return settings;
    }

    public void Save(AppSettingsData settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static Language ResolveLanguage(AppLanguagePreference preference)
        => preference switch
        {
            AppLanguagePreference.English => Language.English,
            AppLanguagePreference.Vietnamese => Language.Vietnamese,
            _ => CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                ? Language.Vietnamese
                : Language.English
        };

    public static AppThemePreference ResolveTheme(AppThemePreference preference)
    {
        if (preference is AppThemePreference.Light or AppThemePreference.Dark)
        {
            return preference;
        }

        return WindowsThemeProbe.IsDarkModePreferred()
            ? AppThemePreference.Dark
            : AppThemePreference.Light;
    }

    public static UpdateChannel DefaultUpdateChannel => UpdateChannel.Prerelease;
}

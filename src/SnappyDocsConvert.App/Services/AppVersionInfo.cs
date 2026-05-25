using System.Reflection;

namespace SnappyDocsConvert.App.Services;

public static class AppVersionInfo
{
    public const string SourceRepository = "https://github.com/KiritoMainBro88/snappy-docs-convert";

    public const string ReleasesPage = "https://github.com/KiritoMainBro88/snappy-docs-convert/releases";

    public const string DiscordUrl = "https://discord.gg/kZ3U36ncun";

    public static string AppName => "kmb file tools";

    public static string Version
        => typeof(AppVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.1.0-beta.2";

    public static string Commit
    {
        get
        {
            var version = Version;
            var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
            return plusIndex >= 0 && plusIndex < version.Length - 1
                ? version[(plusIndex + 1)..]
                : "unavailable";
        }
    }
}

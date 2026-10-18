using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnappyDocsConvert.App.Services;

/// <summary>
/// Theme service: swaps the semantic palette (Styles/Theme.Light.xaml vs Theme.Dark.xaml)
/// as a merged dictionary so every DynamicResource resolves consistently, then keeps the
/// native title bar in sync via DWM immersive dark mode. Local-only.
/// </summary>
public sealed class ThemeService
{
    private static readonly Uri LightSource = new("pack://application:,,,/Styles/Theme.Light.xaml");
    private static readonly Uri DarkSource = new("pack://application:,,,/Styles/Theme.Dark.xaml");

    public AppThemePreference EffectiveTheme { get; private set; } = AppThemePreference.Light;

    public void Apply(AppThemePreference preference)
    {
        EffectiveTheme = AppSettingsService.ResolveTheme(preference);
        var dark = EffectiveTheme == AppThemePreference.Dark;
        SwapPalette(dark ? DarkSource : LightSource);
        ApplyTitleBar(dark);
        TraceApply(dark);
    }

    private static void TraceApply(bool dark)
    {
        try
        {
            if (Application.Current is null)
            {
                return;
            }

            var line = $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff} themeApply dark={dark} effective={dark} " +
                       $"mergedCount={Application.Current.Resources.MergedDictionaries.Count} " +
                       $"bg={Application.Current.Resources["AppBackgroundBrush"]}";
            File.AppendAllText(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "kmb-file-tools", "logs", "theme.log"),
                line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics only; never break theme application.
        }
    }

    /// <summary>Replaces the palette dictionary in its existing merged-dictionary slot so the
    /// lookup order is untouched and exactly one palette stays in the tree.</summary>
    private static void SwapPalette(Uri source)
    {
        if (Application.Current is null)
        {
            return;
        }

        var resources = Application.Current.Resources.MergedDictionaries;
        var index = PaletteIndex(resources, "Theme.Light.xaml");
        if (index < 0)
        {
            index = PaletteIndex(resources, "Theme.Dark.xaml");
        }

        if (index >= 0)
        {
            var target = source == DarkSource ? "Theme.Dark.xaml" : "Theme.Light.xaml";
            if ((resources[index].Source?.OriginalString ?? "").EndsWith(target, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var replacement = new ResourceDictionary { Source = source };
        if (index >= 0)
        {
            resources[index] = replacement;
        }
        else
        {
            resources.Insert(0, replacement);
        }
    }

    private static int PaletteIndex(System.Collections.ObjectModel.Collection<ResourceDictionary> resources, string fileName)
    {
        for (var i = 0; i < resources.Count; i++)
        {
            var name = resources[i].Source?.OriginalString.Split('/', '\\') is { Length: > 0 } parts
                ? parts[^1]
                : "";
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ApplyTitleBar(bool dark)
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBarToWindow(window, dark);
        }
    }

    /// <summary>
    /// Asks DWM to render the native title bar in dark/light to match the in-app theme, so the OS
    /// chrome no longer stays white over a dark UI. Best-effort: silently ignored on OS builds that
    /// do not support the attribute. Local-only, no network.
    /// </summary>
    public static void ApplyTitleBarToWindow(Window window, bool dark)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                // Window not yet sourced; apply once the HWND exists.
                window.SourceInitialized -= OnSourceInitialized;
                window.SourceInitialized += OnSourceInitialized;
                return;
            }

            var useDark = dark ? 1 : 0;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (1903+); older builds used 19.
            if (DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, 19, ref useDark, sizeof(int));
            }
        }
        catch
        {
            // Title bar theming is cosmetic; never let it break theme application.
        }
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.SourceInitialized -= OnSourceInitialized;
            var dark = ThemeIsDark();
            ApplyTitleBarToWindow(window, dark);
        }
    }

    private static bool ThemeIsDark()
        => Application.Current?.Resources["AppBackgroundBrush"] is SolidColorBrush brush
            && brush.Color.R < 0x80;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}

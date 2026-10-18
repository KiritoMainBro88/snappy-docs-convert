using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnappyDocsConvert.App.Services;

public sealed class ThemeService
{
    public AppThemePreference EffectiveTheme { get; private set; } = AppThemePreference.Light;

    public void Apply(AppThemePreference preference)
    {
        EffectiveTheme = AppSettingsService.ResolveTheme(preference);
        var dark = EffectiveTheme == AppThemePreference.Dark;

        SetBrush("AppBackgroundBrush", dark ? "#0D1117" : "#F6F7F9");
        SetBrush("AppForegroundBrush", dark ? "#F4F7FB" : "#101828");
        SetBrush("PanelBrush", dark ? "#111827" : "#FFFFFF");
        SetBrush("SoftPanelBrush", dark ? "#172033" : "#FAFBFC");
        SetBrush("BorderBrushSoft", dark ? "#334155" : "#D0D5DD");
        SetBrush("AccentBrush", dark ? "#66A3FF" : "#1D4ED8");
        SetBrush("AccentSoftBrush", dark ? "#122849" : "#EFF6FF");
        SetBrush("MutedBrush", dark ? "#AAB6C6" : "#667085");
        SetBrush("SuccessBrush", dark ? "#61D394" : "#027A48");
        SetBrush("SuccessSoftBrush", dark ? "#0F2D23" : "#ECFDF3");
        SetBrush("WarningBrush", dark ? "#FFC66D" : "#B54708");
        SetBrush("WarningSoftBrush", dark ? "#33240F" : "#FFFAEB");
        SetBrush("ErrorBrush", dark ? "#FF9A93" : "#B42318");
        SetBrush("ErrorSoftBrush", dark ? "#341817" : "#FEF3F2");

        ApplyTitleBar(dark);
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

    private static void SetBrush(string key, string color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                return;
            }

            brush.Color = (Color)ColorConverter.ConvertFromString(color);
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}

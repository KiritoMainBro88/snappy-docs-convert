using System.Windows;
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
    }

    private static void SetBrush(string key, string color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = (Color)ColorConverter.ConvertFromString(color);
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}

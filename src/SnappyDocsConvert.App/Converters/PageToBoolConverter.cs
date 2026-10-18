using System.Globalization;
using System.Windows.Data;
using SnappyDocsConvert.App.ViewModels;

namespace SnappyDocsConvert.App.Converters;

/// <summary>
/// Converts AppPage enum ⇄ bool so sidebar radio buttons share one selected state without
/// duplicating each page in the window code-behind. Two-way: checking a radio selects the page.
/// </summary>
public sealed class PageToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only "checked" carries meaning; unchecked radio asks for no change.
        if (value is true && Enum.TryParse(parameter?.ToString(), out AppPage page))
        {
            return page;
        }

        return Binding.DoNothing;
    }
}

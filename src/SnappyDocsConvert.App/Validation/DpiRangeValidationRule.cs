using System.Globalization;
using System.Windows.Controls;

namespace SnappyDocsConvert.App.Validation;

/// <summary>
/// Inline DPI validation for the input DPI field: keeps user input within the engine's
/// accepted 72–600 range so the error is visible while typing rather than only at Start.
/// The Start guard in the view model remains as the authoritative backstop.
/// </summary>
public sealed class DpiRangeValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo culture)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.ValidResult;
        }

        if (!int.TryParse(value.ToString(), NumberStyles.Integer, culture, out var dpi))
        {
            return new ValidationResult(false, "DPI phải là số nguyên / DPI must be a whole number.");
        }

        if (dpi is < 72 or > 600)
        {
            return new ValidationResult(false, "DPI phải trong 72–600 / DPI must be 72–600.");
        }

        return ValidationResult.ValidResult;
    }
}

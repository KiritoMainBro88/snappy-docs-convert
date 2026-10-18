using System.Reflection;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Per-file AutomationSecurity envelope for Office automation. Microsoft's guidance is to save and
/// restore the application's automation security state around file opens — including on a shared
/// instance and even when the operation fails.
///
/// - During the body: <c>msoAutomationSecurityForceDisable (3)</c> so macros inside converted
///   documents never execute and no macro security dialog can block automation.
/// - After the body (finally): the previous value is restored, so a shared Office instance the user
///   is actively using is never left in a modified security state.
///
/// Documented limitation: Microsoft has called out Excel 4.0 (XLM) macros as an edge case that
/// AutomationSecurity may not fully block; this helper cannot change engine behavior for those.
/// </summary>
public static class OfficeAutomationSecurity
{
    public const int ForceDisable = 3; // msoAutomationSecurityForceDisable

    /// <summary>
    /// Runs <paramref name="body"/> with ForceDisable engaged, then restores the prior state even on
    /// exception. <paramref name="fallbackOriginal"/> is used when the live property cannot be read
    /// (pass the value captured at instance creation).
    /// </summary>
    public static void RunWithForceDisabled(object? app, int? fallbackOriginal, Action body)
    {
        if (app is null)
        {
            body();
            return;
        }

        int? original = null;
        var changed = false;
        try
        {
            original = TryGetInt(app, "AutomationSecurity");
            var restoreValue = original ?? fallbackOriginal;
            if (original != ForceDisable)
            {
                if (TrySet(app, ForceDisable))
                {
                    changed = true;
                }
            }

            body();
        }
        finally
        {
            var restoreValue = original ?? fallbackOriginal;
            if (changed && restoreValue.HasValue && restoreValue.Value != ForceDisable)
            {
                TrySet(app, restoreValue.Value);
            }
        }
    }

    private static int? TryGetInt(object target, string name)
    {
        try
        {
            var raw = target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
            return raw is null ? null : Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySet(object target, int value)
    {
        try
        {
            target.GetType().InvokeMember(
                "AutomationSecurity",
                BindingFlags.SetProperty,
                null,
                target,
                new object[] { value });
            return true;
        }
        catch
        {
            return false;
        }
    }
}

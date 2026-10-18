using PdfSharp.Fonts;

namespace SnappyDocsConvert.Core.Services.PdfTools;

/// <summary>
/// Minimal <see cref="IFontResolver"/> for PdfSharp v6, which no longer auto-resolves fonts. Maps a
/// small set of family names to a TrueType file found in the Windows Fonts directory, with a sane
/// fallback so text drawing (e.g. watermarks) never throws "No appropriate font found". Local-only;
/// reads font files from disk, no network.
/// </summary>
public sealed class SystemFontResolver : IFontResolver
{
    private static readonly object Gate = new();
    private static bool _registered;

    private static readonly string FontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    // Candidate font files in preference order; first existing one wins as the universal fallback.
    private static readonly string[] FallbackFiles =
    {
        "arial.ttf",
        "segoeui.ttf",
        "calibri.ttf",
        "tahoma.ttf",
        "verdana.ttf"
    };

    /// <summary>Registers this resolver as the PdfSharp global resolver exactly once (idempotent).</summary>
    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new SystemFontResolver();
            }

            _registered = true;
        }
    }

    public byte[]? GetFont(string faceName)
    {
        var path = Path.Combine(FontsDir, faceName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var file = MapFamily(familyName, isBold, isItalic) ?? ResolveFallbackFile();
        return file is null ? null : new FontResolverInfo(file);
    }

    private static string? MapFamily(string familyName, bool isBold, bool isItalic)
    {
        var family = familyName.Trim().ToLowerInvariant();
        var candidates = family switch
        {
            "arial" => Variants("arial", "arialbd", "ariali", "arialbi"),
            "segoe ui" or "segoeui" => Variants("segoeui", "segoeuib", "segoeuii", "segoeuiz"),
            "calibri" => Variants("calibri", "calibrib", "calibrii", "calibriz"),
            "tahoma" => Variants("tahoma", "tahomabd", "tahoma", "tahomabd"),
            "verdana" => Variants("verdana", "verdanab", "verdanai", "verdanaz"),
            _ => null
        };

        if (candidates is null)
        {
            return null;
        }

        var index = (isBold, isItalic) switch
        {
            (true, true) => 3,
            (true, false) => 1,
            (false, true) => 2,
            _ => 0
        };

        var fileName = candidates[index] + ".ttf";
        return File.Exists(Path.Combine(FontsDir, fileName)) ? fileName : null;

        static string[] Variants(string regular, string bold, string italic, string boldItalic)
            => new[] { regular, bold, italic, boldItalic };
    }

    private static string? ResolveFallbackFile()
    {
        foreach (var candidate in FallbackFiles)
        {
            if (File.Exists(Path.Combine(FontsDir, candidate)))
            {
                return candidate;
            }
        }

        return null;
    }
}

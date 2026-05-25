namespace SnappyDocsConvert.Core.Services.Updates;

public static class ReleaseVersionComparer
{
    public static int Compare(string? left, string? right)
    {
        var leftVersion = Parse(left);
        var rightVersion = Parse(right);

        var core = leftVersion.Major.CompareTo(rightVersion.Major);
        if (core != 0) return core;
        core = leftVersion.Minor.CompareTo(rightVersion.Minor);
        if (core != 0) return core;
        core = leftVersion.Patch.CompareTo(rightVersion.Patch);
        if (core != 0) return core;

        if (leftVersion.Prerelease is null && rightVersion.Prerelease is null) return 0;
        if (leftVersion.Prerelease is null) return 1;
        if (rightVersion.Prerelease is null) return -1;

        var label = string.Compare(
            leftVersion.Prerelease.Label,
            rightVersion.Prerelease.Label,
            StringComparison.OrdinalIgnoreCase);
        if (label != 0) return label;

        return leftVersion.Prerelease.Number.CompareTo(rightVersion.Prerelease.Number);
    }

    public static bool IsNewer(string candidate, string current)
        => Compare(candidate, current) > 0;

    private static ParsedVersion Parse(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var plusIndex = text.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            text = text[..plusIndex];
        }

        var prerelease = default(ParsedPrerelease?);
        var dashIndex = text.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            prerelease = ParsePrerelease(text[(dashIndex + 1)..]);
            text = text[..dashIndex];
        }

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ParsedVersion(
            ParseInt(parts, 0),
            ParseInt(parts, 1),
            ParseInt(parts, 2),
            prerelease);
    }

    private static ParsedPrerelease ParsePrerelease(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var label = parts.Length > 0 ? parts[0] : value;
        var number = ParseInt(parts, 1);
        return new ParsedPrerelease(label, number);
    }

    private static int ParseInt(IReadOnlyList<string> parts, int index)
        => parts.Count > index && int.TryParse(parts[index], out var number)
            ? number
            : 0;

    private sealed record ParsedVersion(
        int Major,
        int Minor,
        int Patch,
        ParsedPrerelease? Prerelease);

    private sealed record ParsedPrerelease(string Label, int Number);
}

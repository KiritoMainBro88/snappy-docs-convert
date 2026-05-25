namespace SnappyDocsConvert.Core.Models;

public sealed record ConversionRequest(
    string InputPath,
    string OutputDirectory)
{
    public bool AllowOverwrite { get; init; }

    public TimeSpan? Timeout { get; init; }

    public LibreOfficeOptions Options { get; init; } = new();
}

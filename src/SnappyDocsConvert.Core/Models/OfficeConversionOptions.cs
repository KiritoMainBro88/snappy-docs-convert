namespace SnappyDocsConvert.Core.Models;

public sealed record OfficeConversionOptions
{
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(180);
}

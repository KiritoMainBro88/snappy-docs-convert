namespace SnappyDocsConvert.Core.Models;

public sealed record BatchConversionProgress
{
    public required int TotalItems { get; init; }

    public required int CurrentItemIndex { get; init; }

    public required string CurrentFile { get; init; }

    public required BatchConversionItemStatus Status { get; init; }

    public string? Message { get; init; }

    public double? PercentComplete { get; init; }
}

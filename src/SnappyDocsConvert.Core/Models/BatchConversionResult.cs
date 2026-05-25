namespace SnappyDocsConvert.Core.Models;

public sealed record BatchConversionResult
{
    public required IReadOnlyList<BatchConversionItemResult> Items { get; init; }

    public int TotalItems => Items.Count;

    public int SucceededCount => Items.Count(item => item.Status == BatchConversionItemStatus.Succeeded);

    public int FailedCount => Items.Count(item => item.Status == BatchConversionItemStatus.Failed);

    public int CancelledCount => Items.Count(item => item.Status == BatchConversionItemStatus.Cancelled);

    public int SkippedCount => Items.Count(item => item.Status == BatchConversionItemStatus.Skipped);

    public bool Success => FailedCount == 0 && CancelledCount == 0;

    public TimeSpan Duration { get; init; }
}

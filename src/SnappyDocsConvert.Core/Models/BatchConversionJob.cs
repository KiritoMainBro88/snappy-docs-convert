namespace SnappyDocsConvert.Core.Models;

public sealed record BatchConversionJob(
    IReadOnlyList<BatchConversionItem> Items,
    BatchConversionOptions Options);

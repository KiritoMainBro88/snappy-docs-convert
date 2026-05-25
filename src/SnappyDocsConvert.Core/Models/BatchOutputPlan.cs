namespace SnappyDocsConvert.Core.Models;

public sealed record BatchOutputPlan(
    IReadOnlyList<BatchOutputItemPlan> Items);

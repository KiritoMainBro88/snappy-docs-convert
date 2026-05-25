namespace SnappyDocsConvert.Core.Models;

public enum BatchConversionItemStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Skipped = 5
}

namespace SnappyDocsConvert.Core.Models;

public enum ConversionStatus
{
    Succeeded = 0,
    Failed = 1,
    EngineUnavailable = 2,
    MissingInput = 3,
    UnsupportedInput = 4,
    OutputCollision = 5,
    TimedOut = 6,
    ProcessFailed = 7,
    OutputMissing = 8,
    Cancelled = 9
}

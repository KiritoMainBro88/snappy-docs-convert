namespace SnappyDocsConvert.Core.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

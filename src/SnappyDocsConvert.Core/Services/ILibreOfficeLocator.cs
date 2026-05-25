using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface ILibreOfficeLocator
{
    Task<EngineAvailability> LocateAsync(
        LibreOfficeOptions options,
        CancellationToken cancellationToken);
}

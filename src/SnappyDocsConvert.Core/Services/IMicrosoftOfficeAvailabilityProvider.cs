using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface IMicrosoftOfficeAvailabilityProvider
{
    Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    Task<OfficeAvailability> GetOfficeAvailabilityAsync(CancellationToken cancellationToken);
}

using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public sealed class MicrosoftOfficeAvailabilityProvider : IMicrosoftOfficeAvailabilityProvider
{
    public Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
        => Task.FromResult(EngineAvailability.Unavailable(
            "Microsoft Office COM detection is planned for Phase 3."));
}

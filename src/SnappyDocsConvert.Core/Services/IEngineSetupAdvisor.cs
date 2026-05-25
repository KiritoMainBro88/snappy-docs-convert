using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface IEngineSetupAdvisor
{
    Task<EngineSetupStatus> GetStatusAsync(
        EngineSetupMode mode,
        string? customLibreOfficePath,
        CancellationToken cancellationToken);

    EngineSetupStatus Advise(
        EngineAvailability libreOfficeAvailability,
        EngineAvailability microsoftOfficeAvailability,
        EngineSetupMode mode);
}

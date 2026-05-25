using System.Text.Json;
using System.IO;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.App.Services;

public static class SelfCheckRunner
{
    public static async Task<int> RunAsync(
        AppServiceFactory factory,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var office = await factory.CreateOfficeAvailabilityProvider()
            .GetOfficeAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);
        var libre = await factory.CreateLibreOfficeLocator()
            .LocateAsync(new LibreOfficeOptions { ProbeVersion = false }, cancellationToken)
            .ConfigureAwait(false);

        var payload = new
        {
            app = AppVersionInfo.AppName,
            version = AppVersionInfo.Version,
            commit = AppVersionInfo.Commit,
            selfCheck = "ok",
            wordAvailable = office.WordAvailable,
            powerPointAvailable = office.PowerPointAvailable,
            libreOfficeAvailable = libre.IsAvailable,
            libreOfficePath = libre.ExecutablePath,
            libreOfficeReason = libre.Reason,
            noCloudUpload = true
        };

        await output.WriteLineAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        })).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return 0;
    }
}

using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Core.Services;

public sealed class MicrosoftOfficeAvailabilityProvider : IMicrosoftOfficeAvailabilityProvider
{
    private readonly IComProgIdResolver _progIdResolver;

    public MicrosoftOfficeAvailabilityProvider()
        : this(new ComProgIdResolver())
    {
    }

    public MicrosoftOfficeAvailabilityProvider(IComProgIdResolver progIdResolver)
    {
        _progIdResolver = progIdResolver;
    }

    public async Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var availability = await GetOfficeAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (availability.CanConvertWordDocuments || availability.CanConvertPowerPointPresentations)
        {
            return EngineAvailability.Available("Microsoft Office COM", availability.VersionSummary);
        }

        return EngineAvailability.Unavailable(string.Join(" ", availability.Reasons));
    }

    public Task<OfficeAvailability> GetOfficeAvailabilityAsync(CancellationToken cancellationToken)
    {
        var wordAvailable = _progIdResolver.GetTypeFromProgId(OfficeComProgIds.WordApplication) is not null;
        var powerPointAvailable = _progIdResolver.GetTypeFromProgId(OfficeComProgIds.PowerPointApplication) is not null;
        var excelAvailable = _progIdResolver.GetTypeFromProgId(OfficeComProgIds.ExcelApplication) is not null;
        var reasons = new List<string>();

        if (!wordAvailable)
        {
            reasons.Add("Word.Application ProgID not found.");
        }

        if (!powerPointAvailable)
        {
            reasons.Add("PowerPoint.Application ProgID not found.");
        }

        if (!excelAvailable)
        {
            reasons.Add("Excel.Application ProgID not found.");
        }

        var recommendations = new List<EngineSetupRecommendation>();
        if (!wordAvailable || !powerPointAvailable || !excelAvailable)
        {
            recommendations.Add(new EngineSetupRecommendation(
                EngineSetupRecommendationSeverity.Warning,
                "Microsoft Office not fully detected",
                "Install or repair Microsoft Office, or use LibreOffice fallback for supported files.",
                new[] { new EngineSetupAction("Recheck engines", EngineSetupActionKind.Recheck, null) }));
        }

        return Task.FromResult(new OfficeAvailability(
            wordAvailable,
            powerPointAvailable,
            wordAvailable,
            powerPointAvailable,
            reasons,
            recommendations,
            VersionSummary(wordAvailable, powerPointAvailable, excelAvailable),
            excelAvailable,
            excelAvailable));
    }

    private static string VersionSummary(bool wordAvailable, bool powerPointAvailable, bool excelAvailable)
    {
        var apps = new List<string>();
        if (wordAvailable) apps.Add("Word");
        if (powerPointAvailable) apps.Add("PowerPoint");
        if (excelAvailable) apps.Add("Excel");
        return apps.Count == 0
            ? "Microsoft Office COM unavailable"
            : $"{string.Join(", ", apps)} COM available";
    }
}

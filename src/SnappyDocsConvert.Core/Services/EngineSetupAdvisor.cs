using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.LibreOffice;

namespace SnappyDocsConvert.Core.Services;

public sealed class EngineSetupAdvisor : IEngineSetupAdvisor
{
    private readonly ILibreOfficeLocator _libreOfficeLocator;
    private readonly IMicrosoftOfficeAvailabilityProvider _microsoftOfficeAvailabilityProvider;

    public EngineSetupAdvisor()
        : this(new LibreOfficeLocator(), new MicrosoftOfficeAvailabilityProvider())
    {
    }

    public EngineSetupAdvisor(
        ILibreOfficeLocator libreOfficeLocator,
        IMicrosoftOfficeAvailabilityProvider microsoftOfficeAvailabilityProvider)
    {
        _libreOfficeLocator = libreOfficeLocator;
        _microsoftOfficeAvailabilityProvider = microsoftOfficeAvailabilityProvider;
    }

    public async Task<EngineSetupStatus> GetStatusAsync(
        EngineSetupMode mode,
        string? customLibreOfficePath,
        CancellationToken cancellationToken)
    {
        if (mode == EngineSetupMode.CustomLibreOfficePath && string.IsNullOrWhiteSpace(customLibreOfficePath))
        {
            var officeAvailabilityForMissingPath = await _microsoftOfficeAvailabilityProvider
                .GetAvailabilityAsync(cancellationToken)
                .ConfigureAwait(false);

            return Advise(
                EngineAvailability.Unavailable("Choose soffice.com, soffice.exe, or a LibreOffice install folder."),
                officeAvailabilityForMissingPath,
                mode);
        }

        var libreOfficeOptions = mode == EngineSetupMode.CustomLibreOfficePath
            ? new LibreOfficeOptions { ExecutablePath = customLibreOfficePath }
            : new LibreOfficeOptions();

        var libreOfficeAvailability = await _libreOfficeLocator
            .LocateAsync(libreOfficeOptions, cancellationToken)
            .ConfigureAwait(false);
        var microsoftOfficeAvailability = await _microsoftOfficeAvailabilityProvider
            .GetAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);

        return Advise(libreOfficeAvailability, microsoftOfficeAvailability, mode);
    }

    public EngineSetupStatus Advise(
        EngineAvailability libreOfficeAvailability,
        EngineAvailability microsoftOfficeAvailability,
        EngineSetupMode mode)
    {
        var officeAvailable = microsoftOfficeAvailability.IsAvailable;
        var libreAvailable = libreOfficeAvailability.IsAvailable;

        return mode switch
        {
            EngineSetupMode.IHaveMicrosoftOffice => officeAvailable
                ? ReadyWithOffice(officeAvailable, libreAvailable)
                : MissingSelectedOffice(officeAvailable, libreAvailable),
            EngineSetupMode.IDoNotHaveMicrosoftOffice => libreAvailable
                ? ReadyWithLibreOffice(officeAvailable, libreAvailable)
                : MissingLibreOffice(officeAvailable, libreAvailable),
            EngineSetupMode.UseLibreOffice => libreAvailable
                ? ReadyWithLibreOffice(officeAvailable, libreAvailable)
                : MissingLibreOffice(officeAvailable, libreAvailable),
            EngineSetupMode.CustomLibreOfficePath => libreAvailable
                ? ReadyWithLibreOffice(officeAvailable, libreAvailable)
                : InvalidCustomLibreOfficePath(officeAvailable, libreAvailable, libreOfficeAvailability.Reason),
            _ => Auto(officeAvailable, libreAvailable)
        };
    }

    private static EngineSetupStatus Auto(bool officeAvailable, bool libreAvailable)
    {
        if (officeAvailable)
        {
            return ReadyWithOffice(officeAvailable, libreAvailable);
        }

        if (libreAvailable)
        {
            return ReadyWithLibreOffice(officeAvailable, libreAvailable);
        }

        return NoEnginesAvailable(officeAvailable, libreAvailable);
    }

    private static EngineSetupStatus ReadyWithOffice(bool officeAvailable, bool libreAvailable)
        => new(
            officeAvailable,
            libreAvailable,
            ConversionEngineKind.MicrosoftOffice,
            true,
            null,
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Info,
                    "Microsoft Office engine ready",
                    "Office document conversion can use Microsoft Office for best DOCX/PPTX fidelity.",
                    new[] { UseEngineAction(ConversionEngineKind.MicrosoftOffice) })
            });

    private static EngineSetupStatus ReadyWithLibreOffice(bool officeAvailable, bool libreAvailable)
        => new(
            officeAvailable,
            libreAvailable,
            ConversionEngineKind.LibreOffice,
            true,
            null,
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Info,
                    "LibreOffice engine ready",
                    "Office document conversion can use local LibreOffice headless conversion.",
                    new[]
                    {
                        UseEngineAction(ConversionEngineKind.LibreOffice),
                        LibreOfficeCliHelpAction()
                    })
            });

    private static EngineSetupStatus MissingSelectedOffice(bool officeAvailable, bool libreAvailable)
        => new(
            officeAvailable,
            libreAvailable,
            null,
            false,
            "Microsoft Office was selected but is not available.",
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Warning,
                    "Microsoft Office not detected",
                    "Check the Office installation, recheck engines, or use LibreOffice as fallback.",
                    libreAvailable
                        ? new[] { RecheckAction(), UseEngineAction(ConversionEngineKind.LibreOffice) }
                        : LibreOfficeSetupActions())
            });

    private static EngineSetupStatus MissingLibreOffice(bool officeAvailable, bool libreAvailable)
        => new(
            officeAvailable,
            libreAvailable,
            null,
            false,
            "LibreOffice is required when Microsoft Office is not available.",
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Error,
                    "LibreOffice not detected",
                    "Install LibreOffice from the official site, choose soffice.com manually, then recheck.",
                    LibreOfficeSetupActions())
            });

    private static EngineSetupStatus InvalidCustomLibreOfficePath(
        bool officeAvailable,
        bool libreAvailable,
        string? reason)
        => new(
            officeAvailable,
            libreAvailable,
            null,
            false,
            reason ?? "Selected LibreOffice path is invalid.",
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Error,
                    "Selected LibreOffice path is invalid",
                    reason ?? "Choose soffice.com, soffice.exe, or a LibreOffice install folder.",
                    LibreOfficeSetupActions())
            });

    private static EngineSetupStatus NoEnginesAvailable(bool officeAvailable, bool libreAvailable)
        => new(
            officeAvailable,
            libreAvailable,
            null,
            false,
            "No local Office conversion engine is available.",
            new[]
            {
                new EngineSetupRecommendation(
                    EngineSetupRecommendationSeverity.Error,
                    "No conversion engine detected",
                    "Install LibreOffice or use a machine with Microsoft Office once Phase 3 is implemented.",
                    LibreOfficeSetupActions())
            });

    private static EngineSetupAction[] LibreOfficeSetupActions()
        =>
        [
            new(
                "Download LibreOffice",
                EngineSetupActionKind.OpenUrl,
                KnownExternalToolLinks.LibreOfficeDownload.OfficialUrl),
            new("Choose soffice.com", EngineSetupActionKind.ChooseExecutable, "soffice.com"),
            RecheckAction()
        ];

    private static EngineSetupAction LibreOfficeCliHelpAction()
        => new(
            "LibreOffice CLI help",
            EngineSetupActionKind.OpenUrl,
            KnownExternalToolLinks.LibreOfficeStartParameters.OfficialUrl);

    private static EngineSetupAction RecheckAction()
        => new("Recheck engines", EngineSetupActionKind.Recheck, null);

    private static EngineSetupAction UseEngineAction(ConversionEngineKind engineKind)
        => new($"Use {DisplayName(engineKind)}", EngineSetupActionKind.UseDetectedEngine, engineKind.ToString());

    private static string DisplayName(ConversionEngineKind engineKind)
        => engineKind == ConversionEngineKind.MicrosoftOffice
            ? "Microsoft Office"
            : "LibreOffice";
}

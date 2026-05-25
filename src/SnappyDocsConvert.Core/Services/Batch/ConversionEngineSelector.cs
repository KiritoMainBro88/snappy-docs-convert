using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.LibreOffice;

namespace SnappyDocsConvert.Core.Services.Batch;

public sealed class ConversionEngineSelector : IConversionEngineSelector
{
    private static readonly HashSet<string> OfficeWordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
        ".rtf"
    };

    private static readonly HashSet<string> OfficePowerPointExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ppt",
        ".pptx"
    };

    private static readonly HashSet<string> LibreOfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
        ".rtf",
        ".odt",
        ".ppt",
        ".pptx",
        ".odp"
    };

    private readonly IMicrosoftOfficeAvailabilityProvider _officeAvailabilityProvider;
    private readonly ILibreOfficeLocator _libreOfficeLocator;

    public ConversionEngineSelector()
        : this(new MicrosoftOfficeAvailabilityProvider(), new LibreOfficeLocator())
    {
    }

    public ConversionEngineSelector(
        IMicrosoftOfficeAvailabilityProvider officeAvailabilityProvider,
        ILibreOfficeLocator libreOfficeLocator)
    {
        _officeAvailabilityProvider = officeAvailabilityProvider;
        _libreOfficeLocator = libreOfficeLocator;
    }

    public async Task<BatchEngineSelectionResult> SelectAsync(
        string inputPath,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(inputPath);
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BatchEngineSelectionResult.NoneNeeded();
        }

        if (!IsSupportedDocumentExtension(extension))
        {
            return BatchEngineSelectionResult.Failed($"Unsupported batch input extension '{extension}'.");
        }

        return options.EnginePreference switch
        {
            BatchConversionEnginePreference.MicrosoftOffice => await SelectForcedOfficeAsync(
                extension,
                cancellationToken).ConfigureAwait(false),
            BatchConversionEnginePreference.LibreOffice => await SelectForcedLibreOfficeAsync(
                extension,
                cancellationToken).ConfigureAwait(false),
            _ => await SelectAutoAsync(extension, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<BatchEngineSelectionResult> SelectAutoAsync(
        string extension,
        CancellationToken cancellationToken)
    {
        if (extension is ".odt" or ".odp")
        {
            return await SelectForcedLibreOfficeAsync(extension, cancellationToken).ConfigureAwait(false);
        }

        var officeAvailability = await _officeAvailabilityProvider
            .GetOfficeAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);

        if (OfficeWordExtensions.Contains(extension) && officeAvailability.CanConvertWordDocuments)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        if (OfficePowerPointExtensions.Contains(extension) && officeAvailability.CanConvertPowerPointPresentations)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        var libreOfficeSelection = await SelectForcedLibreOfficeAsync(extension, cancellationToken).ConfigureAwait(false);
        if (libreOfficeSelection.Success)
        {
            return libreOfficeSelection;
        }

        return BatchEngineSelectionResult.Failed(
            $"No local document conversion engine is available for '{extension}'.",
            new[]
            {
                "Install or activate Microsoft Office for DOC/DOCX/RTF/PPT/PPTX.",
                "Install LibreOffice or choose soffice.com for fallback conversion."
            });
    }

    private async Task<BatchEngineSelectionResult> SelectForcedOfficeAsync(
        string extension,
        CancellationToken cancellationToken)
    {
        if (!OfficeWordExtensions.Contains(extension) && !OfficePowerPointExtensions.Contains(extension))
        {
            return BatchEngineSelectionResult.Failed(
                $"Microsoft Office engine does not support '{extension}' in this phase.");
        }

        var availability = await _officeAvailabilityProvider
            .GetOfficeAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);

        if (OfficeWordExtensions.Contains(extension) && availability.CanConvertWordDocuments)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        if (OfficePowerPointExtensions.Contains(extension) && availability.CanConvertPowerPointPresentations)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        return BatchEngineSelectionResult.Failed(
            $"Microsoft Office was selected but required COM app is unavailable for '{extension}'.",
            availability.Reasons);
    }

    private async Task<BatchEngineSelectionResult> SelectForcedLibreOfficeAsync(
        string extension,
        CancellationToken cancellationToken)
    {
        if (!LibreOfficeExtensions.Contains(extension))
        {
            return BatchEngineSelectionResult.Failed(
                $"LibreOffice engine does not support '{extension}' in this phase.");
        }

        var availability = await _libreOfficeLocator
            .LocateAsync(new LibreOfficeOptions { ProbeVersion = false }, cancellationToken)
            .ConfigureAwait(false);

        return availability.IsAvailable
            ? BatchEngineSelectionResult.Selected(ConversionEngineKind.LibreOffice)
            : BatchEngineSelectionResult.Failed(
                $"LibreOffice was selected but is unavailable for '{extension}'.",
                new[] { availability.Reason ?? "Install LibreOffice or choose soffice.com." });
    }

    private static bool IsSupportedDocumentExtension(string extension)
        => LibreOfficeExtensions.Contains(extension)
            || OfficeWordExtensions.Contains(extension)
            || OfficePowerPointExtensions.Contains(extension);
}

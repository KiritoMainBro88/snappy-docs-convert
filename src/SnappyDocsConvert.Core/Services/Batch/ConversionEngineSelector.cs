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

    private static readonly HashSet<string> OfficeExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
        ".csv"
    };

    private static readonly HashSet<string> LibreOfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
        ".rtf",
        ".odt",
        ".ppt",
        ".pptx",
        ".odp",
        ".xls",
        ".xlsx",
        ".csv",
        ".ods"
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
                options,
                cancellationToken).ConfigureAwait(false),
            _ => await SelectAutoAsync(extension, options, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<BatchEngineSelectionResult> SelectAutoAsync(
        string extension,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        if (extension is ".odt" or ".odp" or ".ods")
        {
            return await SelectForcedLibreOfficeAsync(extension, options, cancellationToken).ConfigureAwait(false);
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

        if (OfficeExcelExtensions.Contains(extension) && officeAvailability.CanConvertExcelWorkbooks)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        var libreOfficeSelection = await SelectForcedLibreOfficeAsync(extension, options, cancellationToken).ConfigureAwait(false);
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
        if (!OfficeWordExtensions.Contains(extension)
            && !OfficePowerPointExtensions.Contains(extension)
            && !OfficeExcelExtensions.Contains(extension))
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

        if (OfficeExcelExtensions.Contains(extension) && availability.CanConvertExcelWorkbooks)
        {
            return BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice);
        }

        return BatchEngineSelectionResult.Failed(
            $"Microsoft Office was selected but required COM app is unavailable for '{extension}'.",
            availability.Reasons);
    }

    private async Task<BatchEngineSelectionResult> SelectForcedLibreOfficeAsync(
        string extension,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        if (!LibreOfficeExtensions.Contains(extension))
        {
            return BatchEngineSelectionResult.Failed(
                $"LibreOffice engine does not support '{extension}' in this phase.");
        }

        var availability = await _libreOfficeLocator
            .LocateAsync(
                new LibreOfficeOptions
                {
                    ExecutablePath = options.LibreOfficeExecutablePath,
                    ProbeVersion = false
                },
                cancellationToken)
            .ConfigureAwait(false);

        return availability.IsAvailable
            ? BatchEngineSelectionResult.Selected(ConversionEngineKind.LibreOffice)
            : BatchEngineSelectionResult.Failed(
                $"LibreOffice was selected but is unavailable for '{extension}'.",
                new[] { availability.Reason ?? "Install LibreOffice or choose soffice.com." });
    }

    public async Task<BatchEngineEligibilityResult> GetEligibleEnginesAsync(
        string inputPath,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(inputPath);

        // No engine is needed for PDF inputs, and unsupported extensions have no eligible engine.
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            || !IsSupportedDocumentExtension(extension))
        {
            return BatchEngineEligibilityResult.Empty;
        }

        var officeEligible = await IsOfficeEligibleAsync(extension, cancellationToken).ConfigureAwait(false);
        var libreOfficeEligible = await IsLibreOfficeEligibleAsync(extension, options, cancellationToken)
            .ConfigureAwait(false);

        // Preference encodes the ordering so the first element is the natural primary and any later
        // element is a candidate fallback. LibreOffice goes first only when explicitly forced.
        var officeFirst = options.EnginePreference != BatchConversionEnginePreference.LibreOffice;

        var ordered = new List<ConversionEngineKind>(2);
        if (officeFirst)
        {
            if (officeEligible)
            {
                ordered.Add(ConversionEngineKind.MicrosoftOffice);
            }

            if (libreOfficeEligible)
            {
                ordered.Add(ConversionEngineKind.LibreOffice);
            }
        }
        else
        {
            if (libreOfficeEligible)
            {
                ordered.Add(ConversionEngineKind.LibreOffice);
            }

            if (officeEligible)
            {
                ordered.Add(ConversionEngineKind.MicrosoftOffice);
            }
        }

        return BatchEngineEligibilityResult.From(ordered);
    }

    private async Task<bool> IsOfficeEligibleAsync(string extension, CancellationToken cancellationToken)
    {
        var isWord = OfficeWordExtensions.Contains(extension);
        var isPowerPoint = OfficePowerPointExtensions.Contains(extension);
        var isExcel = OfficeExcelExtensions.Contains(extension);
        if (!isWord && !isPowerPoint && !isExcel)
        {
            return false;
        }

        var availability = await _officeAvailabilityProvider
            .GetOfficeAvailabilityAsync(cancellationToken)
            .ConfigureAwait(false);

        return (isWord && availability.CanConvertWordDocuments)
            || (isPowerPoint && availability.CanConvertPowerPointPresentations)
            || (isExcel && availability.CanConvertExcelWorkbooks);
    }

    private async Task<bool> IsLibreOfficeEligibleAsync(
        string extension,
        BatchConversionOptions options,
        CancellationToken cancellationToken)
    {
        if (!LibreOfficeExtensions.Contains(extension))
        {
            return false;
        }

        var availability = await _libreOfficeLocator
            .LocateAsync(
                new LibreOfficeOptions
                {
                    ExecutablePath = options.LibreOfficeExecutablePath,
                    ProbeVersion = false
                },
                cancellationToken)
            .ConfigureAwait(false);

        return availability.IsAvailable;
    }

    private static bool IsSupportedDocumentExtension(string extension)
        => LibreOfficeExtensions.Contains(extension)
            || OfficeWordExtensions.Contains(extension)
            || OfficePowerPointExtensions.Contains(extension)
            || OfficeExcelExtensions.Contains(extension);
}

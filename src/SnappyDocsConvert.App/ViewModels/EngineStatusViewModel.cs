namespace SnappyDocsConvert.App.ViewModels;

/// <summary>
/// Presentation state of one convert engine's availability. Values are produced by
/// detection (registry / path probing) and are language-independent - localized text
/// is derived from the state, never the other way round.
/// </summary>
public enum EngineAvailabilityState
{
    /// <summary>Detection in flight (also the state before the first check finishes).</summary>
    Checking,

    /// <summary>The probe found a usable installation.</summary>
    Available,

    /// <summary>The probe found no usable installation.</summary>
    Missing,
}

/// <summary>State of the engines-page setup guidance line. Core supplies reason text.</summary>
public enum EngineSetupState
{
    /// <summary>A recheck is running.</summary>
    Checking,

    /// <summary>The setup advisor reported no blocking reason.</summary>
    Ready,

    /// <summary>The setup advisor reported a blocking reason (untranslated Core data).</summary>
    Blocked,

    /// <summary>The recheck itself threw.</summary>
    Failed,
}

/// <summary>
/// Typed engine status for the Engines page. Holds one availability state per engine
/// plus the setup-advisor verdict; display labels are derived from the states and
/// re-derived when the shell language changes.
/// </summary>
public sealed class EngineStatusViewModel : ObservableObject
{
    private readonly Localization.LocalizationService _localization;

    private EngineAvailabilityState _wordState = EngineAvailabilityState.Checking;
    private EngineAvailabilityState _powerPointState = EngineAvailabilityState.Checking;
    private EngineAvailabilityState _libreOfficeState = EngineAvailabilityState.Checking;
    private EngineSetupState _setupState = EngineSetupState.Checking;

    private string _libreOfficePath = "";
    private string _guidanceText = "";
    private string _wordLabel = "";
    private string _powerPointLabel = "";
    private string _libreOfficeLabel = "";

    // EngineSetupAdvisor verdict data (kept untranslated; only state drives presentation).
    private string? _blockingReason;
    private string _recommendationText = "";
    private string? _failureMessage;

    public EngineStatusViewModel(Localization.LocalizationService localization)
    {
        _localization = localization;
        _localization.PropertyChanged += (_, _) => Refresh();
        Refresh();
    }

    public EngineAvailabilityState WordState
    {
        get => _wordState;
        set
        {
            if (SetProperty(ref _wordState, value))
            {
                WordLabel = LabelFor(value);
            }
        }
    }

    public EngineAvailabilityState PowerPointState
    {
        get => _powerPointState;
        set
        {
            if (SetProperty(ref _powerPointState, value))
            {
                PowerPointLabel = LabelFor(value);
            }
        }
    }

    public EngineAvailabilityState LibreOfficeState
    {
        get => _libreOfficeState;
        set
        {
            if (SetProperty(ref _libreOfficeState, value))
            {
                LibreOfficeLabel = LabelFor(value);
            }
        }
    }

    /// <summary>
    /// The PDF renderer ships inside the app (PDFtoImage/PDFium, fully in-process) so no
    /// runtime probe exists. The card asserts bundled availability like before, now typed.
    /// </summary>
    public EngineAvailabilityState PdfRendererState => EngineAvailabilityState.Available;

    public EngineSetupState SetupState
    {
        get => _setupState;
        private set => SetProperty(ref _setupState, value);
    }

    public string LibreOfficePath
    {
        get => _libreOfficePath;
        set => SetProperty(ref _libreOfficePath, value);
    }

    public string WordLabel
    {
        get => _wordLabel;
        private set => SetProperty(ref _wordLabel, value);
    }

    public string PowerPointLabel
    {
        get => _powerPointLabel;
        private set => SetProperty(ref _powerPointLabel, value);
    }

    public string LibreOfficeLabel
    {
        get => _libreOfficeLabel;
        private set => SetProperty(ref _libreOfficeLabel, value);
    }

    private string _pdfRendererLabel = "";

    public string PdfRendererLabel
    {
        get => _pdfRendererLabel;
        private set => SetProperty(ref _pdfRendererLabel, value);
    }

    public string GuidanceText
    {
        get => _guidanceText;
        private set => SetProperty(ref _guidanceText, value);
    }

    /// <summary>Marks the setup guidance as "checking in progress" while a recheck runs.</summary>
    public void SetSetupChecking()
    {
        _blockingReason = null;
        _recommendationText = "";
        SetupState = EngineSetupState.Checking;
        Refresh();
    }

    /// <summary>Stores the Core setup-advisor verdict and recomputes the guidance line.</summary>
    public void SetSetupResult(string? blockingReason, string recommendationsText)
    {
        _blockingReason = blockingReason;
        _recommendationText = recommendationsText;
        SetupState = string.IsNullOrWhiteSpace(blockingReason)
            ? EngineSetupState.Ready
            : EngineSetupState.Blocked;
        Refresh();
    }

    /// <summary>Records that the whole recheck threw (message kept as raw data).</summary>
    public void SetSetupFailed(string failureMessage)
    {
        _blockingReason = null;
        _recommendationText = "";
        _failureMessage = failureMessage;
        SetupState = EngineSetupState.Failed;
        Refresh();
    }

    private void Refresh()
    {
        WordLabel = LabelFor(WordState);
        PowerPointLabel = LabelFor(PowerPointState);
        LibreOfficeLabel = LabelFor(LibreOfficeState);
        PdfRendererLabel = LabelFor(PdfRendererState);
        GuidanceText = _setupState switch
        {
            EngineSetupState.Checking => _localization["CheckingEngines"],
            EngineSetupState.Blocked => _blockingReason ?? "",
            EngineSetupState.Failed => _failureMessage is null
                ? ""
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _localization["EngineCheckFailed"],
                    _failureMessage),
            _ => _recommendationText
        };
    }

    private string LabelFor(EngineAvailabilityState state) => state switch
    {
        EngineAvailabilityState.Available => _localization["Available"],
        EngineAvailabilityState.Missing => _localization["Missing"],
        _ => _localization["Checking"]
    };
}

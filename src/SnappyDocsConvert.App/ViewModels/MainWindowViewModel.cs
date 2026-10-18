using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SnappyDocsConvert.App.Localization;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Models.Updates;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Updates;

namespace SnappyDocsConvert.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IShellPageHost
{
    private static readonly IReadOnlySet<string> PdfToolPdfExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" };

    private static readonly IReadOnlySet<string> PdfToolImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp"
        };

    private readonly FilePickerService _filePicker;
    private readonly FolderPickerService _folderPicker;
    private readonly IBatchInputScanner _inputScanner;
    private readonly AppServiceFactory _serviceFactory;
    private readonly AppSettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly AppUpdateService _updateService;
    private readonly AppSettingsData _settings;

    private bool _isRunning;
    private string? _customLibreOfficePath;
    private string _summaryText;
    private PdfToolOperation _selectedPdfToolOperation = PdfToolOperation.Merge;
    private PdfRotationAngle _selectedPdfRotationAngle = PdfRotationAngle.Degrees90;
    private string _pdfToolPageRanges = "";
    private string _pdfToolOutputFileName = "";
    private string _pdfToolWatermarkText = "";
    private string _pdfToolPassword = "";
    private string _pdfToolStatus = "";
    private string _alertText = "";
    private AppPage _selectedPage = AppPage.Convert;
    private CancellationTokenSource? _runCancellation;
    private bool _wordAvailable;
    private bool _powerPointAvailable;
    private bool _libreOfficeAvailable;
    private AppLanguagePreference _selectedLanguagePreference;
    private AppThemePreference _selectedThemePreference;
    private UpdateChannel _selectedUpdateChannel;
    private string _effectiveThemeText = "";
    private string _latestVersion = "";
    private string _releaseNotesPreview = "";
    private string _updateStatus = "";
    private double _updateDownloadProgress;
    private string _downloadedUpdatePath = "";
    private AppUpdateAsset? _selectedUpdateAsset;
    private string _releasePageUrl = AppVersionInfo.ReleasesPage;

    public MainWindowViewModel(AppServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _settingsService = serviceFactory.CreateAppSettingsService();
        _themeService = serviceFactory.CreateThemeService();
        _updateService = serviceFactory.CreateAppUpdateService();
        _settings = _settingsService.Load();
        _selectedLanguagePreference = _settings.Language;
        _selectedThemePreference = _settings.Theme;
        _selectedUpdateChannel = _settings.UpdateChannel;
        _filePicker = serviceFactory.CreateFilePickerService();
        _folderPicker = serviceFactory.CreateFolderPickerService();
        _inputScanner = serviceFactory.CreateBatchInputScanner();
        EngineStatus = new EngineStatusViewModel(Loc);

        // The convert page owns its queue/options/commands; the shell stays the single host
        // facade for runs, settings persistence, pickers, logs, alerts and shared engine
        // state, so there is exactly one pipeline and one run gate for the whole app.
        Convert = new ConvertPageViewModel(
            this,
            _inputScanner,
            AppSettingsService.ResolveLanguage(_settings.Language));

        ApplyPersistedAppearance();
        _summaryText = Loc["Ready"];
        _pdfToolStatus = Loc["PdfToolsReady"];

        ClearLogCommand = new RelayCommand(ClearLog, () => LogLines.Count > 0);
        CopyLogsCommand = new RelayCommand(CopyLogs, () => LogLines.Count > 0);
        AddPdfToolFilesCommand = new RelayCommand(AddPdfToolFiles, () => !IsRunning);
        ClearPdfToolInputsCommand = new RelayCommand(ClearPdfToolInputs, () => !IsRunning && PdfToolInputs.Count > 0);
        RunPdfToolCommand = new AsyncRelayCommand(RunPdfToolAsync, () => !IsRunning);
        RecheckEnginesCommand = new AsyncRelayCommand(RecheckEnginesAsync);
        ChooseLibreOfficeCommand = new AsyncRelayCommand(ChooseLibreOfficeAsync, () => !IsRunning);
        OpenLibreOfficeDownloadCommand = new RelayCommand(OpenLibreOfficeDownload);

        ShowConvertCommand = new RelayCommand(() => SelectedPage = AppPage.Convert);
        ShowPdfToolsCommand = new RelayCommand(() => SelectedPage = AppPage.PdfTools);
        ShowEnginesCommand = new RelayCommand(() => SelectedPage = AppPage.Engines);
        ShowLogsCommand = new RelayCommand(() => SelectedPage = AppPage.Logs);
        ShowSettingsCommand = new RelayCommand(() => SelectedPage = AppPage.Settings);
        ShowHelpCommand = new RelayCommand(() => SelectedPage = AppPage.Help);
        UseSystemLanguageCommand = new RelayCommand(() => SetLanguagePreference(AppLanguagePreference.System));
        UseEnglishCommand = new RelayCommand(() => SetLanguagePreference(AppLanguagePreference.English));
        UseVietnameseCommand = new RelayCommand(() => SetLanguagePreference(AppLanguagePreference.Vietnamese));
        UseSystemThemeCommand = new RelayCommand(() => SetThemePreference(AppThemePreference.System));
        UseLightThemeCommand = new RelayCommand(() => SetThemePreference(AppThemePreference.Light));
        UseDarkThemeCommand = new RelayCommand(() => SetThemePreference(AppThemePreference.Dark));
        UseStableUpdatesCommand = new RelayCommand(() => SetUpdateChannel(UpdateChannel.Stable));
        UsePrereleaseUpdatesCommand = new RelayCommand(() => SetUpdateChannel(UpdateChannel.Prerelease));
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsRunning);
        DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync, () => !IsRunning && _selectedUpdateAsset is not null);
        InstallUpdateCommand = new RelayCommand(InstallUpdate, () => !IsRunning && !string.IsNullOrWhiteSpace(DownloadedUpdatePath));
        OpenReleasePageCommand = new RelayCommand(() => OpenUrl(_releasePageUrl));
        OpenSourceCommand = new RelayCommand(() => OpenUrl(AppVersionInfo.SourceRepository));
        OpenDiscordCommand = new RelayCommand(() => OpenUrl(AppVersionInfo.DiscordUrl));

        LogoPath = FindLogoPath();
        _updateStatus = Loc["UpdateReady"];
        RefreshChoices();
        RefreshEngineStatusLabels();
        _ = RecheckEnginesAsync();
    }

    public LocalizationService Loc { get; } = new();

    // The convert page view model owns the queue, formatting options and page commands.
    public ConvertPageViewModel Convert { get; }

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> PdfToolInputs { get; } = new();

    public ObservableCollection<string> RejectedPaths { get; } = new();

    public ObservableCollection<ChoiceItem<PdfToolOperation>> PdfToolOperationChoices { get; } = new();

    public ObservableCollection<ChoiceItem<PdfRotationAngle>> PdfRotationChoices { get; } = new();

    public EngineStatusViewModel EngineStatus { get; private set; } = null!;

    public RelayCommand ClearLogCommand { get; }

    public RelayCommand CopyLogsCommand { get; }

    public RelayCommand AddPdfToolFilesCommand { get; }

    public RelayCommand ClearPdfToolInputsCommand { get; }

    public AsyncRelayCommand RunPdfToolCommand { get; }

    public AsyncRelayCommand RecheckEnginesCommand { get; }

    public AsyncRelayCommand ChooseLibreOfficeCommand { get; }

    public RelayCommand OpenLibreOfficeDownloadCommand { get; }

    public RelayCommand ShowConvertCommand { get; }

    public RelayCommand ShowPdfToolsCommand { get; }

    public RelayCommand ShowEnginesCommand { get; }

    public RelayCommand ShowLogsCommand { get; }

    public RelayCommand ShowSettingsCommand { get; }

    public RelayCommand ShowHelpCommand { get; }

    public RelayCommand UseSystemLanguageCommand { get; }

    public RelayCommand UseEnglishCommand { get; }

    public RelayCommand UseVietnameseCommand { get; }

    public RelayCommand UseSystemThemeCommand { get; }

    public RelayCommand UseLightThemeCommand { get; }

    public RelayCommand UseDarkThemeCommand { get; }

    public RelayCommand UseStableUpdatesCommand { get; }

    public RelayCommand UsePrereleaseUpdatesCommand { get; }

    public AsyncRelayCommand CheckForUpdatesCommand { get; }

    public AsyncRelayCommand DownloadUpdateCommand { get; }

    public RelayCommand InstallUpdateCommand { get; }

    public RelayCommand OpenReleasePageCommand { get; }

    public RelayCommand OpenSourceCommand { get; }

    public RelayCommand OpenDiscordCommand { get; }

    public string LogoPath { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsNotRunning));
                InvokeRunStateChanged();
            }
        }
    }

    public void InvokeRunStateChanged()
    {
        RaiseCommandStates();
        RunStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsNotRunning => !IsRunning;

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public PdfToolOperation SelectedPdfToolOperation
    {
        get => _selectedPdfToolOperation;
        set
        {
            if (SetProperty(ref _selectedPdfToolOperation, value))
            {
                OnPropertyChanged(nameof(IsPdfToolPageRangeVisible));
                OnPropertyChanged(nameof(IsPdfToolRotationVisible));
                OnPropertyChanged(nameof(IsPdfToolWatermarkVisible));
                OnPropertyChanged(nameof(IsPdfToolPasswordVisible));
            }
        }
    }

    public PdfRotationAngle SelectedPdfRotationAngle
    {
        get => _selectedPdfRotationAngle;
        set => SetProperty(ref _selectedPdfRotationAngle, value);
    }

    public string PdfToolPageRanges
    {
        get => _pdfToolPageRanges;
        set => SetProperty(ref _pdfToolPageRanges, value);
    }

    public string PdfToolOutputFileName
    {
        get => _pdfToolOutputFileName;
        set => SetProperty(ref _pdfToolOutputFileName, value);
    }

    public string PdfToolWatermarkText
    {
        get => _pdfToolWatermarkText;
        set => SetProperty(ref _pdfToolWatermarkText, value);
    }

    public string PdfToolPassword
    {
        get => _pdfToolPassword;
        set => SetProperty(ref _pdfToolPassword, value);
    }

    public string PdfToolStatus
    {
        get => _pdfToolStatus;
        set => SetProperty(ref _pdfToolStatus, value);
    }

    public string AlertText
    {
        get => _alertText;
        set
        {
            if (SetProperty(ref _alertText, value))
            {
                OnPropertyChanged(nameof(HasAlert));
            }
        }
    }

    public bool HasAlert => !string.IsNullOrWhiteSpace(AlertText);

    public AppPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                OnPropertyChanged(nameof(CurrentModeTitle));
                OnPropertyChanged(nameof(CurrentModeDescription));
            }
        }
    }

    public string CurrentModeTitle => SelectedPage switch
    {
        AppPage.PdfTools => Loc["ModePdfToolsTitle"],
        AppPage.Engines => Loc["ModeEnginesTitle"],
        AppPage.Logs => Loc["ModeLogsTitle"],
        AppPage.Settings => Loc["ModeSettingsTitle"],
        AppPage.Help => Loc["ModeHelpTitle"],
        _ => Loc["ModeConvertTitle"]
    };

    public string CurrentModeDescription => SelectedPage switch
    {
        AppPage.PdfTools => Loc["ModePdfToolsDescription"],
        AppPage.Engines => Loc["ModeEnginesDescription"],
        AppPage.Logs => Loc["ModeLogsDescription"],
        AppPage.Settings => Loc["ModeSettingsDescription"],
        AppPage.Help => Loc["ModeHelpDescription"],
        _ => Loc["ModeConvertDescription"]
    };

    public string CurrentVersion => AppVersionInfo.Version;

    public string CurrentCommit => AppVersionInfo.Commit;

    public AppLanguagePreference SelectedLanguagePreference
    {
        get => _selectedLanguagePreference;
        private set => SetProperty(ref _selectedLanguagePreference, value);
    }

    public AppThemePreference SelectedThemePreference
    {
        get => _selectedThemePreference;
        private set => SetProperty(ref _selectedThemePreference, value);
    }

    public UpdateChannel SelectedUpdateChannel
    {
        get => _selectedUpdateChannel;
        private set => SetProperty(ref _selectedUpdateChannel, value);
    }

    public string EffectiveThemeText
    {
        get => _effectiveThemeText;
        private set => SetProperty(ref _effectiveThemeText, value);
    }

    public string LatestVersion
    {
        get => _latestVersion;
        private set => SetProperty(ref _latestVersion, value);
    }

    public string ReleaseNotesPreview
    {
        get => _releaseNotesPreview;
        private set => SetProperty(ref _releaseNotesPreview, value);
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public double UpdateDownloadProgress
    {
        get => _updateDownloadProgress;
        private set => SetProperty(ref _updateDownloadProgress, value);
    }

    public string DownloadedUpdatePath
    {
        get => _downloadedUpdatePath;
        private set => SetProperty(ref _downloadedUpdatePath, value);
    }

    public bool IsPdfToolPageRangeVisible =>
        SelectedPdfToolOperation is PdfToolOperation.ExtractPages or PdfToolOperation.RotatePages;

    public bool IsPdfToolRotationVisible => SelectedPdfToolOperation == PdfToolOperation.RotatePages;

    public bool IsPdfToolWatermarkVisible => SelectedPdfToolOperation == PdfToolOperation.Watermark;

    public bool IsPdfToolPasswordVisible => SelectedPdfToolOperation == PdfToolOperation.Encrypt;

    public void AddPdfToolPaths(IEnumerable<string> paths)
    {
        if (IsRunning)
        {
            ShowAlert(Loc["WaitCurrentTask"]);
            Log("PDF tool add blocked: task running.");
            return;
        }

        var existing = PdfToolInputs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var rejected = 0;

        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var fullPath = Path.GetFullPath(path);
            if (!IsPdfToolInputAllowed(fullPath))
            {
                rejected++;
                AddRejectedPath(fullPath, $"Not valid for {SelectedPdfToolOperation}");
                Log($"PDF tool rejected input for {SelectedPdfToolOperation}: {fullPath}");
                continue;
            }

            if (!existing.Add(fullPath))
            {
                // Duplicate within this call or an already-added input: log only. The rejected
                // list is reserved for inputs that are not valid for the chosen operation.
                Log($"PDF tool duplicate ignored: {fullPath}");
                continue;
            }

            PdfToolInputs.Add(fullPath);
            added++;
        }

        if (rejected > 0)
        {
            ShowAlert($"{rejected} PDF tool input(s) rejected for {SelectedPdfToolOperation}.");
        }

        PdfToolStatus = $"{PdfToolInputs.Count} PDF tool input(s) ready.";
        Log($"PDF tools added {added} input(s).");
        RaiseCommandStates();
    }

    private void AddPdfToolFiles() => AddPdfToolPaths(_filePicker.PickPdfToolFiles());

    private void ClearPdfToolInputs()
    {
        PdfToolInputs.Clear();
        PdfToolStatus = Loc["PdfToolsReady"];
        Log("PDF tool inputs cleared.");
        RaiseCommandStates();
    }

    private async Task RunPdfToolAsync()
    {
        if (PdfToolInputs.Count == 0)
        {
            PdfToolStatus = Loc["PdfToolNoInputs"];
            ShowAlert(PdfToolStatus);
            Log("PDF tool blocked: no inputs.");
            return;
        }

        var invalidInput = PdfToolInputs.FirstOrDefault(path => !IsPdfToolInputAllowed(path));
        if (!string.IsNullOrWhiteSpace(invalidInput))
        {
            PdfToolStatus = $"Input not valid for {SelectedPdfToolOperation}: {invalidInput}";
            ShowAlert(PdfToolStatus);
            Log(PdfToolStatus);
            return;
        }

        if (string.IsNullOrWhiteSpace(Convert.OutputFolder))
        {
            PdfToolStatus = Loc["PdfToolChooseOutput"];
            ShowAlert(PdfToolStatus);
            Log("PDF tool blocked: output folder is required.");
            return;
        }

        IsRunning = true;
        _runCancellation = new CancellationTokenSource();
        PdfToolStatus = Loc["Running"];

        try
        {
            Directory.CreateDirectory(Convert.OutputFolder);
            Log($"PDF tool started: {SelectedPdfToolOperation}.");
            AlertText = "";
            var token = _runCancellation.Token;

            // PDF -> Word is a reverse conversion handled by Word COM, not the PdfSharp tool service.
            if (SelectedPdfToolOperation == PdfToolOperation.OcrToText)
            {
                var ocr = _serviceFactory.CreateOcrService(_settings.PaddleOcrPath);
                if (ocr is null)
                {
                    PdfToolStatus = Loc["OcrEngineMissing"];
                    ShowAlert(PdfToolStatus);
                    Log("OCR blocked: PaddleOCR path not configured.");
                    return;
                }

                var outputs = new List<string>();
                var failures = new List<string>();
                foreach (var path in PdfToolInputs.ToArray())
                {
                    token.ThrowIfCancellationRequested();
                    var ocrResult = await Task.Run(
                            () => ocr.RecognizeAsync(path, Convert.OutputFolder, Convert.Dpi, token),
                            token)
                        .ConfigureAwait(true);
                    if (!ocrResult.Success)
                    {
                        // One file's failure must not discard the rest of the batch: keep going and
                        // report per-file outcomes in the final summary.
                        failures.Add($"{Path.GetFileName(path)}: {ocrResult.ErrorMessage ?? "OCR failed."}");
                        Log($"OCR failed: {failures[^1]}");
                        continue;
                    }

                    outputs.Add(ocrResult.OutputTextPath);
                }

                if (failures.Count > 0)
                {
                    if (outputs.Count == 0)
                    {
                        PdfToolStatus = $"OCR failed for all {failures.Count} input(s). {failures[0]}";
                        ShowAlert(PdfToolStatus);
                        Log(PdfToolStatus);
                        return;
                    }

                    PdfToolStatus = $"OCR done with {failures.Count} failure(s): {outputs.Count}/{PdfToolInputs.Count} text file(s) written. Failed: {string.Join(" | ", failures)}";
                    ShowAlert(PdfToolStatus);
                    Log(PdfToolStatus);
                    return;
                }

                PdfToolStatus = $"OCR done. {outputs.Count} text file(s). First: {outputs.FirstOrDefault() ?? Convert.OutputFolder}";
                Log(PdfToolStatus);
                return;
            }

            if (SelectedPdfToolOperation == PdfToolOperation.PdfToWord)
            {
                var converter = _serviceFactory.CreatePdfToDocumentConverter();
                try
                {
                    var outputs = new List<string>();
                    foreach (var pdf in PdfToolInputs.ToArray())
                    {
                        token.ThrowIfCancellationRequested();
                        var conv = await Task.Run(
                                () => converter.ConvertToDocxAsync(pdf, Convert.OutputFolder, token),
                                token)
                            .ConfigureAwait(true);
                        if (!conv.Success)
                        {
                            PdfToolStatus = conv.ErrorMessage ?? "PDF to Word failed.";
                            ShowAlert(PdfToolStatus);
                            Log($"PDF to Word failed: {PdfToolStatus}");
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(conv.OutputPdfPath))
                        {
                            outputs.Add(conv.OutputPdfPath);
                        }
                    }

                    PdfToolStatus = $"PDF to Word done. {outputs.Count} file(s). First: {outputs.FirstOrDefault() ?? Convert.OutputFolder}";
                    Log(PdfToolStatus);
                }
                finally
                {
                    converter.DisposeSessionIfOwned();
                }

                return;
            }

            var request = new PdfToolRequest
            {
                Operation = SelectedPdfToolOperation,
                InputPaths = PdfToolInputs.ToArray(),
                OutputDirectory = Convert.OutputFolder,
                OutputFileName = string.IsNullOrWhiteSpace(PdfToolOutputFileName)
                    ? null
                    : PdfToolOutputFileName,
                PageRanges = string.IsNullOrWhiteSpace(PdfToolPageRanges)
                    ? null
                    : PdfToolPageRanges,
                RotationAngle = SelectedPdfRotationAngle,
                WatermarkText = string.IsNullOrWhiteSpace(PdfToolWatermarkText)
                    ? null
                    : PdfToolWatermarkText,
                UserPassword = string.IsNullOrEmpty(PdfToolPassword)
                    ? null
                    : PdfToolPassword
            };

            var service = _serviceFactory.CreatePdfToolService();
            var result = await Task.Run(
                    () => service.RunAsync(request, token),
                    token)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                PdfToolStatus = result.ErrorMessage ?? "PDF tool failed.";
                ShowAlert(PdfToolStatus);
                Log($"PDF tool failed: {PdfToolStatus}");
                return;
            }

            var firstOutput = result.OutputFiles.FirstOrDefault() ?? Convert.OutputFolder;
            PdfToolStatus = $"PDF tool done. {result.OutputFiles.Count} output file(s). First: {firstOutput}";
            Log(PdfToolStatus);
            foreach (var warning in result.Warnings.Take(3))
            {
                Log($"PDF tool warning: {warning}");
            }
        }
        catch (OperationCanceledException)
        {
            PdfToolStatus = "PDF tool cancelled.";
            Log(PdfToolStatus);
        }
        catch (Exception ex)
        {
            PdfToolStatus = $"PDF tool error: {ex.Message}";
            ShowAlert(PdfToolStatus);
            Log(PdfToolStatus);
        }
        finally
        {
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

    private void ClearLog()
    {
        LogLines.Clear();
        ClearLogCommand.RaiseCanExecuteChanged();
        CopyLogsCommand.RaiseCanExecuteChanged();
    }

    private void CopyLogs()
    {
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, LogLines));
            Log("Logs copied.");
        }
        catch (Exception ex)
        {
            Log($"Copy logs failed: {ex.Message}");
        }
    }

    private async Task RecheckEnginesAsync()
    {
        try
        {
            EngineStatus.SetSetupChecking();
            var office = await _serviceFactory.CreateOfficeAvailabilityProvider()
                .GetOfficeAvailabilityAsync(CancellationToken.None)
                .ConfigureAwait(true);
            var libre = await _serviceFactory.CreateLibreOfficeLocator()
                .LocateAsync(
                    new LibreOfficeOptions
                    {
                        ExecutablePath = _customLibreOfficePath,
                        ProbeVersion = false
                    },
                    CancellationToken.None)
                .ConfigureAwait(true);
            var mode = string.IsNullOrWhiteSpace(_customLibreOfficePath)
                ? EngineSetupMode.Auto
                : EngineSetupMode.CustomLibreOfficePath;
            var setup = await _serviceFactory.CreateEngineSetupAdvisor()
                .GetStatusAsync(mode, _customLibreOfficePath, CancellationToken.None)
                .ConfigureAwait(true);

            _wordAvailable = office.WordAvailable;
            _powerPointAvailable = office.PowerPointAvailable;
            _libreOfficeAvailable = libre.IsAvailable;
            RefreshEngineStatusLabels();
            EngineStatus.LibreOfficePath = libre.ExecutablePath ?? libre.Reason ?? "No LibreOffice path detected.";
            EngineStatus.SetSetupResult(
                setup.BlockingReason,
                string.Join(" ", setup.Recommendations.Select(recommendation => recommendation.Message)));

            // The convert page listens to availability changes so its guidance line stays
            // current without the shell owning page state.
            EngineAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            Log("Engine status checked.");
        }
        catch (Exception ex)
        {
            EngineStatus.SetSetupFailed(ex.Message);
            Log($"Engine check failed: {ex.Message}");
        }
    }

    private async Task ChooseLibreOfficeAsync()
    {
        var path = _filePicker.PickLibreOfficeExecutable();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _customLibreOfficePath = path;
        Log($"LibreOffice path selected for this session: {path}");
        await RecheckEnginesAsync().ConfigureAwait(true);
    }

    private static void OpenLibreOfficeDownload()
    {
        Process.Start(new ProcessStartInfo(KnownExternalToolLinks.LibreOfficeDownloadUrl)
        {
            UseShellExecute = true
        });
    }

    private void RefreshEngineStatusLabels()
    {
        // Typed states are the single status source; labels are derived inside
        // EngineStatusViewModel and re-derived automatically on language changes.
        EngineStatus.WordState = _wordAvailable
            ? EngineAvailabilityState.Available
            : EngineAvailabilityState.Missing;
        EngineStatus.PowerPointState = _powerPointAvailable
            ? EngineAvailabilityState.Available
            : EngineAvailabilityState.Missing;
        EngineStatus.LibreOfficeState = _libreOfficeAvailable
            ? EngineAvailabilityState.Available
            : EngineAvailabilityState.Missing;

        // PdfRendererState is a typed constant (bundled in-process renderer).
    }

    private void ApplyPersistedAppearance()
    {
        SetLanguage(AppSettingsService.ResolveLanguage(_settings.Language));
        _themeService.Apply(_settings.Theme);
        EffectiveThemeText = _themeService.EffectiveTheme == AppThemePreference.Dark
            ? Loc["ThemeDark"]
            : Loc["ThemeLight"];
    }

    private void SetLanguagePreference(AppLanguagePreference preference)
    {
        SelectedLanguagePreference = preference;
        _settings.Language = preference;
        _settingsService.Save(_settings);
        SetLanguage(AppSettingsService.ResolveLanguage(preference));
        Log($"Language preference set: {preference}.");
    }

    private void SetThemePreference(AppThemePreference preference)
    {
        SelectedThemePreference = preference;
        _settings.Theme = preference;
        _settingsService.Save(_settings);
        _themeService.Apply(preference);
        EffectiveThemeText = _themeService.EffectiveTheme == AppThemePreference.Dark
            ? Loc["ThemeDark"]
            : Loc["ThemeLight"];
        Log($"Theme preference set: {preference}; effective: {_themeService.EffectiveTheme}.");
    }

    private void SetUpdateChannel(UpdateChannel channel)
    {
        SelectedUpdateChannel = channel;
        _settings.UpdateChannel = channel;
        _settingsService.Save(_settings);
        UpdateStatus = channel == UpdateChannel.Prerelease
            ? Loc["UpdateChannelPrerelease"]
            : Loc["UpdateChannelStable"];
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateStatus = Loc["UpdateChecking"];
        LatestVersion = "";
        ReleaseNotesPreview = "";
        DownloadedUpdatePath = "";
        _selectedUpdateAsset = null;
        DownloadUpdateCommand.RaiseCanExecuteChanged();
        InstallUpdateCommand.RaiseCanExecuteChanged();

        var result = await _updateService
            .CheckAsync(CurrentVersion, SelectedUpdateChannel, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Success)
        {
            UpdateStatus = result.ErrorMessage ?? Loc["UpdateCheckFailed"];
            ShowAlert(UpdateStatus);
            Log(UpdateStatus);
            return;
        }

        if (!result.UpdateAvailable)
        {
            UpdateStatus = Loc["UpdateNone"];
            Log(UpdateStatus);
            return;
        }

        LatestVersion = result.LatestVersion ?? "";
        _releasePageUrl = result.ReleasePageUrl ?? AppVersionInfo.ReleasesPage;
        ReleaseNotesPreview = TrimReleaseNotes(result.ReleaseNotes);
        _selectedUpdateAsset = result.PreferredAsset;
        UpdateStatus = _selectedUpdateAsset is null
            ? Loc["UpdateFoundNoAsset"]
            : string.Format(Loc["UpdateFound"], LatestVersion, _selectedUpdateAsset.Name);
        Log(UpdateStatus);
        DownloadUpdateCommand.RaiseCanExecuteChanged();
    }

    private async Task DownloadUpdateAsync()
    {
        if (_selectedUpdateAsset is null)
        {
            UpdateStatus = Loc["UpdateFoundNoAsset"];
            return;
        }

        UpdateDownloadProgress = 0d;
        UpdateStatus = Loc["UpdateDownloading"];
        var versionFolder = string.IsNullOrWhiteSpace(LatestVersion) ? "latest" : LatestVersion;
        var targetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "kmb-file-tools",
            "updates",
            SanitizePathSegment(versionFolder));

        try
        {
            var progress = new Progress<double>(value => UpdateDownloadProgress = value);
            DownloadedUpdatePath = await _updateService
                .DownloadAsync(_selectedUpdateAsset, targetDirectory, progress, CancellationToken.None)
                .ConfigureAwait(true);
            UpdateStatus = string.Format(Loc["UpdateDownloaded"], DownloadedUpdatePath);
            Log(UpdateStatus);
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Download failed: {ex.Message}";
            ShowAlert(UpdateStatus);
            Log(UpdateStatus);
        }
        finally
        {
            InstallUpdateCommand.RaiseCanExecuteChanged();
        }
    }

    private void InstallUpdate()
    {
        if (string.IsNullOrWhiteSpace(DownloadedUpdatePath) || !File.Exists(DownloadedUpdatePath))
        {
            UpdateStatus = Loc["UpdateDownloadFirst"];
            ShowAlert(UpdateStatus);
            return;
        }

        if (DownloadedUpdatePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DownloadedUpdatePath,
                UseShellExecute = true
            });
            Application.Current.Shutdown();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetDirectoryName(DownloadedUpdatePath) ?? DownloadedUpdatePath,
            UseShellExecute = true
        });
        UpdateStatus = Loc["UpdateZipInstruction"];
    }

    private static string TrimReleaseNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "";
        }

        var normalized = notes.Replace("\r\n", "\n", StringComparison.Ordinal);
        return normalized.Length <= 1200 ? normalized : normalized[..1200] + "...";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowAlert($"Open failed: {ex.Message}");
        }
    }

    private void SetLanguage(Language language)
    {
        Loc.SetLanguage(language);
        RefreshChoices();
        RefreshEngineStatusLabels();

        // Pages keep their own localization instance and translated choices.
        Convert.OnShellLanguageChanged(language);
        OnPropertyChanged(nameof(CurrentModeTitle));
        OnPropertyChanged(nameof(CurrentModeDescription));
        OnPropertyChanged(nameof(EffectiveThemeText));
        if (PdfToolStatus == LocalizedStrings.Get(language == Language.English ? Language.Vietnamese : Language.English, "PdfToolsReady"))
        {
            PdfToolStatus = Loc["PdfToolsReady"];
        }
        if (UpdateStatus == LocalizedStrings.Get(language == Language.English ? Language.Vietnamese : Language.English, "UpdateReady"))
        {
            UpdateStatus = Loc["UpdateReady"];
        }
    }

    private void RefreshChoices()
    {
        ReplaceChoices(
            PdfToolOperationChoices,
            Enum.GetValues<PdfToolOperation>()
                .Select(value => new ChoiceItem<PdfToolOperation>(value, PdfToolLabel(value))));
        ReplaceChoices(
            PdfRotationChoices,
            Enum.GetValues<PdfRotationAngle>()
                .Select(value => new ChoiceItem<PdfRotationAngle>(value, RotationLabel(value))));
    }

    private string PdfToolLabel(PdfToolOperation operation) => operation switch
    {
        PdfToolOperation.Merge => Loc["ToolMerge"],
        PdfToolOperation.Split => Loc["ToolSplit"],
        PdfToolOperation.ExtractPages => Loc["ToolExtract"],
        PdfToolOperation.RotatePages => Loc["ToolRotate"],
        PdfToolOperation.ImagesToPdf => Loc["ToolImagesToPdf"],
        PdfToolOperation.Compress => Loc["ToolCompress"],
        PdfToolOperation.Watermark => Loc["ToolWatermark"],
        PdfToolOperation.Encrypt => Loc["ToolEncrypt"],
        PdfToolOperation.PdfToWord => Loc["ToolPdfToWord"],
        PdfToolOperation.OcrToText => Loc["ToolOcr"],
        _ => operation.ToString()
    };

    private string RotationLabel(PdfRotationAngle angle) => angle switch
    {
        PdfRotationAngle.Degrees90 => Loc["Rotation90"],
        PdfRotationAngle.Degrees180 => Loc["Rotation180"],
        PdfRotationAngle.Degrees270 => Loc["Rotation270"],
        _ => angle.ToString()
    };

    private static void ReplaceChoices<T>(
        ObservableCollection<ChoiceItem<T>> target,
        IEnumerable<ChoiceItem<T>> choices)
    {
        target.Clear();
        foreach (var choice in choices)
        {
            target.Add(choice);
        }
    }

    private bool IsPdfToolInputAllowed(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (SelectedPdfToolOperation == PdfToolOperation.ImagesToPdf)
        {
            return PdfToolImageExtensions.Contains(extension);
        }

        // OCR accepts both PDF and image inputs.
        if (SelectedPdfToolOperation == PdfToolOperation.OcrToText)
        {
            return PdfToolPdfExtensions.Contains(extension)
                || PdfToolImageExtensions.Contains(extension);
        }

        return PdfToolPdfExtensions.Contains(extension);
    }

    public void AddRejectedPath(string path, string reason)
    {
        var entry = $"{reason}: {path}";
        if (!RejectedPaths.Contains(entry))
        {
            RejectedPaths.Add(entry);
        }
    }

    // IShellPageHost facade: page view models funnel every shared concern through these
    // members so the shell stays the single owner of run gating, settings, logging and
    // alerts.
    public void ShowAlert(string message)
    {
        AlertText = message;
        SummaryText = message;
    }

    public void ClearAlert() => AlertText = "";

    public void SetSummaryText(string text) => SummaryText = text;

    public void ClearRejectedPaths() => RejectedPaths.Clear();

    public void Log(string message)
    {
        LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (LogLines.Count > 300)
        {
            LogLines.RemoveAt(0);
        }

        ClearLogCommand.RaiseCanExecuteChanged();
        CopyLogsCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        ChooseLibreOfficeCommand.RaiseCanExecuteChanged();
        ClearLogCommand.RaiseCanExecuteChanged();
        CopyLogsCommand.RaiseCanExecuteChanged();
        AddPdfToolFilesCommand.RaiseCanExecuteChanged();
        ClearPdfToolInputsCommand.RaiseCanExecuteChanged();
        RunPdfToolCommand.RaiseCanExecuteChanged();
        CheckForUpdatesCommand.RaiseCanExecuteChanged();
        DownloadUpdateCommand.RaiseCanExecuteChanged();
        InstallUpdateCommand.RaiseCanExecuteChanged();
    }

    // Raised on the UI thread after an engine recheck refreshed availability.
    public event EventHandler? EngineAvailabilityChanged;

    // Raised whenever IsRunning changed (a run started or ended).
    public event EventHandler? RunStateChanged;

    // ---- IShellPageHost implementation --------------------------------------------

    public bool IsRunActive => IsRunning;

    /// <summary>
    /// Single-run gate shared by the convert page and the PDF tools page: null means a run
    /// is already active. Disposing the lease reproduces the previous inline finally blocks
    /// exactly (end IsRunning, dispose and clear the cancellation source).
    /// </summary>
    public IRunLease? TryBeginRun()
    {
        if (IsRunning)
        {
            return null;
        }

        IsRunning = true;
        _runCancellation = new CancellationTokenSource();
        return new PageRunLease(this);
    }

    public void RequestCancelRun() => _runCancellation?.Cancel();

    public string? CustomLibreOfficePath => _customLibreOfficePath;

    public bool EngineFallbackPreference => _settings.EnableEngineFallback;

    public void PersistEngineFallbackPreference(bool enabled)
    {
        _settings.EnableEngineFallback = enabled;
        _settingsService.Save(_settings);
    }

    public IBatchInputScanner InputScanner => _inputScanner;

    public UiBatchRunner CreateBatchRunner() => _serviceFactory.CreateBatchRunner();

    public IReadOnlyList<string> PickInputFiles() => _filePicker.PickInputFiles();

    public string? PickFolder(string title) => _folderPicker.PickFolder(title);

    public string GetEngineGuidance(BatchConversionEnginePreference preference) => preference switch
    {
        BatchConversionEnginePreference.MicrosoftOffice =>
            _wordAvailable || _powerPointAvailable
                ? Loc["OfficeGuidanceAvailable"]
                : Loc["OfficeGuidanceMissing"],
        BatchConversionEnginePreference.LibreOffice =>
            _libreOfficeAvailable
                ? Loc["LibreGuidanceAvailable"]
                : Loc["LibreGuidanceMissing"],
        _ => Loc["AutoGuidance"]
    };

    private sealed class PageRunLease(MainWindowViewModel owner) : IRunLease
    {
        public CancellationToken Token => owner._runCancellation!.Token;

        public void Dispose()
        {
            owner._runCancellation?.Dispose();
            owner._runCancellation = null;
            owner.IsRunning = false;
        }
    }

    private static string FindLogoPath()
    {
        var outputLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        return File.Exists(outputLogo) ? outputLogo : "";
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SnappyDocsConvert.App.Localization;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Models.Updates;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.Updates;

namespace SnappyDocsConvert.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
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
    private readonly UiBatchRunner _batchRunner;
    private readonly AppServiceFactory _serviceFactory;
    private readonly AppSettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly AppUpdateService _updateService;
    private readonly AppSettingsData _settings;

    private QueueItemViewModel? _selectedQueueItem;
    private string _outputFolder = "";
    private BatchConversionTarget _selectedTarget = BatchConversionTarget.PdfAndImages;
    private BatchConversionEnginePreference _selectedEngine = BatchConversionEnginePreference.Auto;
    private ImageOutputFormat _selectedImageFormat = ImageOutputFormat.Png;
    private int _dpi = 200;
    private bool _keepIntermediatePdf;
    private bool _isRunning;
    private string? _customLibreOfficePath;
    private string _summaryText;
    private string _selectedEngineGuidance = "";
    private PdfToolOperation _selectedPdfToolOperation = PdfToolOperation.Merge;
    private PdfRotationAngle _selectedPdfRotationAngle = PdfRotationAngle.Degrees90;
    private string _pdfToolPageRanges = "";
    private string _pdfToolOutputFileName = "";
    private string _pdfToolStatus = "";
    private string _alertText = "";
    private AppPage _selectedPage = AppPage.Convert;
    private CancellationTokenSource? _runCancellation;
    private bool _wordAvailable;
    private bool _powerPointAvailable;
    private bool _libreOfficeAvailable;
    private bool _engineStatusKnown;
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
        ApplyPersistedAppearance();
        _filePicker = serviceFactory.CreateFilePickerService();
        _folderPicker = serviceFactory.CreateFolderPickerService();
        _inputScanner = serviceFactory.CreateBatchInputScanner();
        _batchRunner = serviceFactory.CreateBatchRunner();
        _summaryText = Loc["Ready"];
        _pdfToolStatus = Loc["PdfToolsReady"];

        AddFilesCommand = new RelayCommand(AddFiles, () => !IsRunning);
        AddFolderCommand = new RelayCommand(AddFolder, () => !IsRunning);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => !IsRunning && SelectedQueueItem is not null);
        ClearCommand = new RelayCommand(ClearQueue, () => !IsRunning && Queue.Count > 0);
        ChooseOutputFolderCommand = new RelayCommand(ChooseOutputFolder, () => !IsRunning);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        OpenItemOutputCommand = new RelayCommand<QueueItemViewModel>(OpenItemOutput, item => !IsRunning && item is not null && !string.IsNullOrWhiteSpace(item.Output));
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
        RefreshSelectedEngineGuidance();
        _ = RecheckEnginesAsync();
    }

    public LocalizationService Loc { get; } = new();

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> PdfToolInputs { get; } = new();

    public ObservableCollection<string> RejectedPaths { get; } = new();

    public ObservableCollection<ChoiceItem<BatchConversionTarget>> TargetChoices { get; } = new();

    public ObservableCollection<ChoiceItem<BatchConversionEnginePreference>> EngineChoices { get; } = new();

    public ObservableCollection<ChoiceItem<ImageOutputFormat>> ImageFormatChoices { get; } = new();

    public ObservableCollection<ChoiceItem<PdfToolOperation>> PdfToolOperationChoices { get; } = new();

    public ObservableCollection<ChoiceItem<PdfRotationAngle>> PdfRotationChoices { get; } = new();

    public EngineStatusViewModel EngineStatus { get; } = new();

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand ChooseOutputFolderCommand { get; }

    public AsyncRelayCommand StartCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand OpenOutputFolderCommand { get; }

    public RelayCommand<QueueItemViewModel> OpenItemOutputCommand { get; }

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

    public QueueItemViewModel? SelectedQueueItem
    {
        get => _selectedQueueItem;
        set
        {
            if (SetProperty(ref _selectedQueueItem, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set => SetProperty(ref _outputFolder, value);
    }

    public BatchConversionTarget SelectedTarget
    {
        get => _selectedTarget;
        set => SetProperty(ref _selectedTarget, value);
    }

    public BatchConversionEnginePreference SelectedEngine
    {
        get => _selectedEngine;
        set
        {
            if (SetProperty(ref _selectedEngine, value))
            {
                RefreshSelectedEngineGuidance();
            }
        }
    }

    public ImageOutputFormat SelectedImageFormat
    {
        get => _selectedImageFormat;
        set => SetProperty(ref _selectedImageFormat, value);
    }

    public int Dpi
    {
        get => _dpi;
        set => SetProperty(ref _dpi, value);
    }

    public bool KeepIntermediatePdf
    {
        get => _keepIntermediatePdf;
        set => SetProperty(ref _keepIntermediatePdf, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsNotRunning));
                RaiseCommandStates();
            }
        }
    }

    public bool IsNotRunning => !IsRunning;

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public string SelectedEngineGuidance
    {
        get => _selectedEngineGuidance;
        set => SetProperty(ref _selectedEngineGuidance, value);
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

    public void AddPaths(IEnumerable<string> paths)
    {
        if (IsRunning)
        {
            ShowAlert(Loc["WaitCurrentTask"]);
            Log("Add blocked: task running.");
            return;
        }

        var scan = _inputScanner.Scan(paths);
        var existing = Queue
            .Select(item => item.InputPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var filePath in scan.AcceptedFiles)
        {
            if (existing.Contains(filePath))
            {
                AddRejectedPath(filePath, "Duplicate ignored");
                Log($"Duplicate ignored: {filePath}");
                continue;
            }

            Queue.Add(new QueueItemViewModel(filePath, SelectedTarget, SelectedEngine));
            existing.Add(filePath);
            added++;
        }

        foreach (var rejectedPath in scan.RejectedPaths.Take(20))
        {
            AddRejectedPath(rejectedPath, "Unsupported or missing");
            Log($"Unsupported or missing path skipped: {rejectedPath}");
        }

        if (scan.RejectedPaths.Count > 20)
        {
            Log($"Skipped {scan.RejectedPaths.Count - 20} more unsupported paths.");
        }

        if (scan.RejectedPaths.Count > 0)
        {
            ShowAlert($"{scan.RejectedPaths.Count} {Loc["UnsupportedSkipped"]}");
        }
        else if (added > 0)
        {
            AlertText = "";
        }

        SummaryText = $"{Queue.Count} file(s) queued.";
        Log($"Added {added} file(s).");
        RaiseCommandStates();
    }

    private void AddFiles() => AddPaths(_filePicker.PickInputFiles());

    private void AddFolder()
    {
        var folder = _folderPicker.PickFolder("Add folder");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            AddPaths(new[] { folder });
        }
    }

    private void RemoveSelected()
    {
        if (SelectedQueueItem is null)
        {
            return;
        }

        Queue.Remove(SelectedQueueItem);
        SelectedQueueItem = null;
        SummaryText = $"{Queue.Count} file(s) queued.";
        RaiseCommandStates();
    }

    private void ClearQueue()
    {
        Queue.Clear();
        RejectedPaths.Clear();
        AlertText = "";
        SummaryText = "Queue cleared.";
        Log("Queue cleared.");
        RaiseCommandStates();
    }

    private void ChooseOutputFolder()
    {
        var folder = _folderPicker.PickFolder("Select output folder");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputFolder = folder;
            Log($"Output folder: {folder}");
        }
    }

    private async Task StartAsync()
    {
        if (Queue.Count == 0)
        {
            ShowAlert(Loc["AddFilesBeforeStart"]);
            Log("Start blocked: queue is empty.");
            SummaryText = Loc["AddFilesBeforeStart"];
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            ShowAlert(Loc["ChooseOutputBeforeStart"]);
            Log("Start blocked: output folder is required.");
            SummaryText = Loc["ChooseOutputBeforeStart"];
            return;
        }

        if (Dpi is < 72 or > 600)
        {
            ShowAlert(Loc["DpiInvalid"]);
            Log("Start blocked: DPI must be 72-600.");
            SummaryText = Loc["DpiInvalid"];
            return;
        }

        IsRunning = true;
        _runCancellation = new CancellationTokenSource();
        SummaryText = Loc["Running"];

        foreach (var item in Queue)
        {
            item.ApplySettings(SelectedTarget, SelectedEngine);
            item.Status = "Pending";
            item.Message = "";
            item.Output = "";
        }

        try
        {
            Directory.CreateDirectory(OutputFolder);
            Log("Batch started.");
            AlertText = "";

            var job = new BatchConversionJob(
                Queue.Select(item => new BatchConversionItem(item.InputPath)).ToArray(),
                new BatchConversionOptions
                {
                    OutputRoot = OutputFolder,
                    Target = SelectedTarget,
                    EnginePreference = SelectedEngine,
                    ImageFormat = SelectedImageFormat,
                    Dpi = Dpi,
                    JpegQuality = 90,
                    KeepIntermediatePdf = KeepIntermediatePdf,
                    LibreOfficeExecutablePath = _customLibreOfficePath,
                    OverwritePolicy = OverwritePolicy.AutoRename
                });

            var token = _runCancellation.Token;
            var progress = new Progress<BatchConversionProgress>(UpdateProgress);
            var result = await Task.Run(
                    () => _batchRunner.RunAsync(job, progress, token),
                    token)
                .ConfigureAwait(true);

            foreach (var itemResult in result.Items)
            {
                ApplyResult(itemResult);
            }

            SummaryText = $"Done. OK {result.SucceededCount}, failed {result.FailedCount}, cancelled {result.CancelledCount}.";
            Log(SummaryText);
        }
        catch (OperationCanceledException)
        {
            SummaryText = "Batch cancelled.";
            Log("Batch cancelled.");
        }
        catch (Exception ex)
        {
            SummaryText = "Batch failed.";
            ShowAlert(ex.Message);
            Log($"Batch error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

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
                AddRejectedPath(fullPath, "Duplicate ignored");
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

        if (string.IsNullOrWhiteSpace(OutputFolder))
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
            Directory.CreateDirectory(OutputFolder);
            var request = new PdfToolRequest
            {
                Operation = SelectedPdfToolOperation,
                InputPaths = PdfToolInputs.ToArray(),
                OutputDirectory = OutputFolder,
                OutputFileName = string.IsNullOrWhiteSpace(PdfToolOutputFileName)
                    ? null
                    : PdfToolOutputFileName,
                PageRanges = string.IsNullOrWhiteSpace(PdfToolPageRanges)
                    ? null
                    : PdfToolPageRanges,
                RotationAngle = SelectedPdfRotationAngle
            };

            Log($"PDF tool started: {SelectedPdfToolOperation}.");
            AlertText = "";
            var service = _serviceFactory.CreatePdfToolService();
            var token = _runCancellation.Token;
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

            var firstOutput = result.OutputFiles.FirstOrDefault() ?? OutputFolder;
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

    private void Cancel()
    {
        _runCancellation?.Cancel();
        SummaryText = "Cancel requested.";
        Log("Cancel requested.");
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
        {
            ShowAlert(Loc["OutputFolderMissing"]);
            Log("Open output blocked: folder does not exist.");
            return;
        }

        Process.Start(new ProcessStartInfo(OutputFolder)
        {
            UseShellExecute = true
        });
    }

    private void OpenItemOutput(QueueItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Output))
        {
            Log("Open item output blocked: no output yet.");
            return;
        }

        var firstPath = item.Output.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstPath))
        {
            Log("Open item output blocked: output path is empty.");
            return;
        }

        if (Directory.Exists(firstPath))
        {
            Process.Start(new ProcessStartInfo(firstPath) { UseShellExecute = true });
            return;
        }

        if (File.Exists(firstPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{firstPath}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        Log($"Open item output blocked: path not found for {item.FileName}.");
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
            EngineStatus.Guidance = "Checking engines...";
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
            _engineStatusKnown = true;
            RefreshEngineStatusLabels();
            EngineStatus.LibreOfficePath = libre.ExecutablePath ?? libre.Reason ?? "No LibreOffice path detected.";
            EngineStatus.Guidance = setup.BlockingReason
                ?? string.Join(" ", setup.Recommendations.Select(recommendation => recommendation.Message));
            RefreshSelectedEngineGuidance();
            Log("Engine status checked.");
        }
        catch (Exception ex)
        {
            EngineStatus.Guidance = $"Engine check failed: {ex.Message}";
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

    private void UpdateProgress(BatchConversionProgress progress)
    {
        var item = Queue.FirstOrDefault(queueItem =>
            string.Equals(
                Path.GetFullPath(queueItem.InputPath),
                Path.GetFullPath(progress.CurrentFile),
                StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        item.Status = progress.Status.ToString();
        item.Message = progress.Message ?? "";
        SummaryText = $"{progress.CurrentItemIndex + 1}/{progress.TotalItems}: {item.FileName}";
    }

    private void ApplyResult(BatchConversionItemResult result)
    {
        var item = Queue.FirstOrDefault(queueItem =>
            string.Equals(
                Path.GetFullPath(queueItem.InputPath),
                Path.GetFullPath(result.InputPath),
                StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        item.Status = result.Status.ToString();
        item.Message = result.ErrorMessage
            ?? string.Join(" ", result.Warnings.Take(2))
            ?? result.Status.ToString();
        item.Output = result.OutputPdfPath
            ?? result.ImageOutputDirectory
            ?? string.Join("; ", result.ImageFiles.Take(2));
        OpenItemOutputCommand.RaiseCanExecuteChanged();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Log($"{item.FileName}: {result.ErrorMessage}");
        }
    }

    private void RefreshSelectedEngineGuidance()
    {
        SelectedEngineGuidance = SelectedEngine switch
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
    }

    private void RefreshEngineStatusLabels()
    {
        if (!_engineStatusKnown)
        {
            EngineStatus.WordStatus = Loc["Checking"];
            EngineStatus.PowerPointStatus = Loc["Checking"];
            EngineStatus.LibreOfficeStatus = Loc["Checking"];
        }
        else
        {
            EngineStatus.WordStatus = _wordAvailable ? Loc["Available"] : Loc["Missing"];
            EngineStatus.PowerPointStatus = _powerPointAvailable ? Loc["Available"] : Loc["Missing"];
            EngineStatus.LibreOfficeStatus = _libreOfficeAvailable ? Loc["Available"] : Loc["Missing"];
        }

        EngineStatus.PdfRendererStatus = Loc["Available"];
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
        RefreshSelectedEngineGuidance();
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
            TargetChoices,
            Enum.GetValues<BatchConversionTarget>()
                .Select(value => new ChoiceItem<BatchConversionTarget>(value, TargetLabel(value))));
        ReplaceChoices(
            EngineChoices,
            Enum.GetValues<BatchConversionEnginePreference>()
                .Select(value => new ChoiceItem<BatchConversionEnginePreference>(value, EngineLabel(value))));
        ReplaceChoices(
            ImageFormatChoices,
            Enum.GetValues<ImageOutputFormat>()
                .Select(value => new ChoiceItem<ImageOutputFormat>(value, ImageFormatLabel(value))));
        ReplaceChoices(
            PdfToolOperationChoices,
            Enum.GetValues<PdfToolOperation>()
                .Select(value => new ChoiceItem<PdfToolOperation>(value, PdfToolLabel(value))));
        ReplaceChoices(
            PdfRotationChoices,
            Enum.GetValues<PdfRotationAngle>()
                .Select(value => new ChoiceItem<PdfRotationAngle>(value, RotationLabel(value))));
    }

    private string TargetLabel(BatchConversionTarget target) => target switch
    {
        BatchConversionTarget.Pdf => Loc["TargetPdf"],
        BatchConversionTarget.Images => Loc["TargetImages"],
        BatchConversionTarget.PdfAndImages => Loc["TargetPdfAndImages"],
        _ => target.ToString()
    };

    private string EngineLabel(BatchConversionEnginePreference engine) => engine switch
    {
        BatchConversionEnginePreference.Auto => Loc["EngineAuto"],
        BatchConversionEnginePreference.MicrosoftOffice => Loc["EngineMicrosoftOffice"],
        BatchConversionEnginePreference.LibreOffice => Loc["EngineLibreOffice"],
        _ => engine.ToString()
    };

    private string ImageFormatLabel(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Png => Loc["FormatPng"],
        ImageOutputFormat.Jpeg => Loc["FormatJpeg"],
        ImageOutputFormat.Webp => Loc["FormatWebp"],
        _ => format.ToString()
    };

    private string PdfToolLabel(PdfToolOperation operation) => operation switch
    {
        PdfToolOperation.Merge => Loc["ToolMerge"],
        PdfToolOperation.Split => Loc["ToolSplit"],
        PdfToolOperation.ExtractPages => Loc["ToolExtract"],
        PdfToolOperation.RotatePages => Loc["ToolRotate"],
        PdfToolOperation.ImagesToPdf => Loc["ToolImagesToPdf"],
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
        return SelectedPdfToolOperation == PdfToolOperation.ImagesToPdf
            ? PdfToolImageExtensions.Contains(extension)
            : PdfToolPdfExtensions.Contains(extension);
    }

    private void AddRejectedPath(string path, string reason)
    {
        var entry = $"{reason}: {path}";
        if (!RejectedPaths.Contains(entry))
        {
            RejectedPaths.Add(entry);
        }
    }

    private void ShowAlert(string message)
    {
        AlertText = message;
        SummaryText = message;
    }

    private void Log(string message)
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
        AddFilesCommand.RaiseCanExecuteChanged();
        AddFolderCommand.RaiseCanExecuteChanged();
        RemoveSelectedCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
        ChooseOutputFolderCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ChooseLibreOfficeCommand.RaiseCanExecuteChanged();
        OpenItemOutputCommand.RaiseCanExecuteChanged();
        ClearLogCommand.RaiseCanExecuteChanged();
        CopyLogsCommand.RaiseCanExecuteChanged();
        AddPdfToolFilesCommand.RaiseCanExecuteChanged();
        ClearPdfToolInputsCommand.RaiseCanExecuteChanged();
        RunPdfToolCommand.RaiseCanExecuteChanged();
        CheckForUpdatesCommand.RaiseCanExecuteChanged();
        DownloadUpdateCommand.RaiseCanExecuteChanged();
        InstallUpdateCommand.RaiseCanExecuteChanged();
    }

    private static string FindLogoPath()
    {
        var outputLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        return File.Exists(outputLogo) ? outputLogo : "";
    }
}

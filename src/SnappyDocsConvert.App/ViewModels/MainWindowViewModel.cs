using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.PdfTools;

namespace SnappyDocsConvert.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly FilePickerService _filePicker;
    private readonly FolderPickerService _folderPicker;
    private readonly IBatchInputScanner _inputScanner;
    private readonly UiBatchRunner _batchRunner;
    private readonly AppServiceFactory _serviceFactory;

    private QueueItemViewModel? _selectedQueueItem;
    private string _outputFolder = "";
    private BatchConversionTarget _selectedTarget = BatchConversionTarget.PdfAndImages;
    private BatchConversionEnginePreference _selectedEngine = BatchConversionEnginePreference.Auto;
    private ImageOutputFormat _selectedImageFormat = ImageOutputFormat.Png;
    private int _dpi = 200;
    private bool _keepIntermediatePdf;
    private bool _isRunning;
    private string? _customLibreOfficePath;
    private string _summaryText = "Ready. Add files or folders.";
    private string _selectedEngineGuidance = "Auto uses Microsoft Office when available, then LibreOffice fallback. No cloud upload.";
    private PdfToolOperation _selectedPdfToolOperation = PdfToolOperation.Merge;
    private PdfRotationAngle _selectedPdfRotationAngle = PdfRotationAngle.Degrees90;
    private string _pdfToolPageRanges = "";
    private string _pdfToolOutputFileName = "";
    private string _pdfToolStatus = "PDF tools ready. Add local PDF/image files.";
    private CancellationTokenSource? _runCancellation;

    public MainWindowViewModel(AppServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _filePicker = serviceFactory.CreateFilePickerService();
        _folderPicker = serviceFactory.CreateFolderPickerService();
        _inputScanner = serviceFactory.CreateBatchInputScanner();
        _batchRunner = serviceFactory.CreateBatchRunner();

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
        AddPdfToolFilesCommand = new RelayCommand(AddPdfToolFiles, () => !IsRunning);
        ClearPdfToolInputsCommand = new RelayCommand(ClearPdfToolInputs, () => !IsRunning && PdfToolInputs.Count > 0);
        RunPdfToolCommand = new AsyncRelayCommand(RunPdfToolAsync, () => !IsRunning);
        RecheckEnginesCommand = new AsyncRelayCommand(RecheckEnginesAsync);
        ChooseLibreOfficeCommand = new AsyncRelayCommand(ChooseLibreOfficeAsync, () => !IsRunning);
        OpenLibreOfficeDownloadCommand = new RelayCommand(OpenLibreOfficeDownload);

        _ = RecheckEnginesAsync();
    }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> PdfToolInputs { get; } = new();

    public EngineStatusViewModel EngineStatus { get; } = new();

    public IReadOnlyList<BatchConversionTarget> Targets { get; } = Enum.GetValues<BatchConversionTarget>();

    public IReadOnlyList<BatchConversionEnginePreference> Engines { get; } = Enum.GetValues<BatchConversionEnginePreference>();

    public IReadOnlyList<ImageOutputFormat> ImageFormats { get; } = Enum.GetValues<ImageOutputFormat>();

    public IReadOnlyList<PdfToolOperation> PdfToolOperations { get; } = Enum.GetValues<PdfToolOperation>();

    public IReadOnlyList<PdfRotationAngle> PdfRotationAngles { get; } = Enum.GetValues<PdfRotationAngle>();

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

    public RelayCommand AddPdfToolFilesCommand { get; }

    public RelayCommand ClearPdfToolInputsCommand { get; }

    public AsyncRelayCommand RunPdfToolCommand { get; }

    public AsyncRelayCommand RecheckEnginesCommand { get; }

    public AsyncRelayCommand ChooseLibreOfficeCommand { get; }

    public RelayCommand OpenLibreOfficeDownloadCommand { get; }

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
                RaiseCommandStates();
            }
        }
    }

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
        set => SetProperty(ref _selectedPdfToolOperation, value);
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

    public void AddPaths(IEnumerable<string> paths)
    {
        var scan = _inputScanner.Scan(paths);
        var existing = Queue
            .Select(item => item.InputPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var filePath in scan.AcceptedFiles)
        {
            if (existing.Contains(filePath))
            {
                Log($"Duplicate ignored: {filePath}");
                continue;
            }

            Queue.Add(new QueueItemViewModel(filePath, SelectedTarget, SelectedEngine));
            existing.Add(filePath);
            added++;
        }

        foreach (var rejectedPath in scan.RejectedPaths.Take(20))
        {
            Log($"Unsupported or missing path skipped: {rejectedPath}");
        }

        if (scan.RejectedPaths.Count > 20)
        {
            Log($"Skipped {scan.RejectedPaths.Count - 20} more unsupported paths.");
        }

        SummaryText = $"{Queue.Count} file(s) queued.";
        Log($"Added {added} file(s).");
        RaiseCommandStates();
    }

    private void AddFiles()
    {
        AddPaths(_filePicker.PickInputFiles());
    }

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
            Log("Start blocked: queue is empty.");
            SummaryText = "Add files before starting.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            Log("Start blocked: output folder is required.");
            SummaryText = "Choose an output folder.";
            return;
        }

        if (Dpi is < 72 or > 600)
        {
            Log("Start blocked: DPI must be 72-600.");
            SummaryText = "DPI must be 72-600.";
            return;
        }

        IsRunning = true;
        _runCancellation = new CancellationTokenSource();

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

            var progress = new Progress<BatchConversionProgress>(UpdateProgress);
            var result = await _batchRunner
                .RunAsync(job, progress, _runCancellation.Token)
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
            Log($"Batch error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }

    private void AddPdfToolFiles()
    {
        var existing = PdfToolInputs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var path in _filePicker.PickPdfToolFiles())
        {
            if (!existing.Add(path))
            {
                Log($"PDF tool duplicate ignored: {path}");
                continue;
            }

            PdfToolInputs.Add(path);
            added++;
        }

        PdfToolStatus = $"{PdfToolInputs.Count} PDF tool input(s) ready.";
        Log($"PDF tools added {added} input(s).");
        RaiseCommandStates();
    }

    private void ClearPdfToolInputs()
    {
        PdfToolInputs.Clear();
        PdfToolStatus = "PDF tool inputs cleared.";
        Log("PDF tool inputs cleared.");
        RaiseCommandStates();
    }

    private async Task RunPdfToolAsync()
    {
        if (PdfToolInputs.Count == 0)
        {
            PdfToolStatus = "Add PDF/image inputs first.";
            Log("PDF tool blocked: no inputs.");
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            PdfToolStatus = "Choose output folder first.";
            Log("PDF tool blocked: output folder is required.");
            return;
        }

        IsRunning = true;
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
            var result = await _serviceFactory.CreatePdfToolService()
                .RunAsync(request, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                PdfToolStatus = result.ErrorMessage ?? "PDF tool failed.";
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
        catch (Exception ex)
        {
            PdfToolStatus = $"PDF tool error: {ex.Message}";
            Log(PdfToolStatus);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void Cancel()
    {
        _runCancellation?.Cancel();
        Log("Cancel requested.");
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
        {
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

            EngineStatus.WordStatus = office.WordAvailable ? "Available" : "Missing";
            EngineStatus.PowerPointStatus = office.PowerPointAvailable ? "Available" : "Missing";
            EngineStatus.LibreOfficeStatus = libre.IsAvailable ? "Available" : "Missing";
            EngineStatus.PdfRendererStatus = "Available";
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
                EngineStatus.WordStatus == "Available" || EngineStatus.PowerPointStatus == "Available"
                    ? "Microsoft Office mode uses installed Word/PowerPoint. Files needing a missing Office app will fail clearly."
                    : "Microsoft Office mode selected, but Word/PowerPoint are missing. Use Auto or LibreOffice after setup.",
            BatchConversionEnginePreference.LibreOffice =>
                EngineStatus.LibreOfficeStatus == "Available"
                    ? "LibreOffice mode uses detected soffice for supported Office/OpenDocument files."
                    : "LibreOffice mode needs soffice.com or soffice.exe. Choose path or install LibreOffice from official site.",
            _ => "Auto mode uses Microsoft Office first for DOC/PPT when available, then LibreOffice fallback. PDF rendering is local."
        };
    }

    private void Log(string message)
    {
        LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (LogLines.Count > 300)
        {
            LogLines.RemoveAt(0);
        }

        ClearLogCommand.RaiseCanExecuteChanged();
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
        AddPdfToolFilesCommand.RaiseCanExecuteChanged();
        ClearPdfToolInputsCommand.RaiseCanExecuteChanged();
        RunPdfToolCommand.RaiseCanExecuteChanged();
    }
}

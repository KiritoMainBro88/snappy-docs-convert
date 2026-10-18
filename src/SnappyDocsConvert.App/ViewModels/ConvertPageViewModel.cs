using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using SnappyDocsConvert.App.Localization;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.App.ViewModels;

/// <summary>
/// View model of the convert page: queue, output/engine/image formatting options and the
/// convert page commands. Everything application-level (run gate, cancellation, settings
/// persistence, pickers, logging, alerts, shared engine state) stays on the shell and is
/// reached only through <see cref="IShellPageHost"/>, so a run is always entered through
/// the same single token used by the shell and the other pages.
/// </summary>
public sealed class ConvertPageViewModel : ObservableObject
{
    private readonly IShellPageHost _host;
    private readonly IBatchInputScanner _inputScanner;

    private QueueItemViewModel? _selectedQueueItem;
    private string _outputFolder = "";
    private BatchConversionTarget _selectedTarget = BatchConversionTarget.PdfAndImages;
    private BatchConversionEnginePreference _selectedEngine = BatchConversionEnginePreference.Auto;
    private ImageOutputFormat _selectedImageFormat = ImageOutputFormat.Png;
    private int _dpi = 200;
    private bool _keepIntermediatePdf;
    private bool _enableEngineFallback;
    private string _selectedEngineGuidance = "";

    // File-granularity overall progress for the convert queue, 0-100, fed by live pipeline
    // reports in UpdateProgress. There is no per-file intra-file progress anywhere in the
    // pipeline, so this deliberately never displays intermediate smoothness.
    private double _overallPercent;

    public ConvertPageViewModel(
        IShellPageHost host,
        IBatchInputScanner inputScanner,
        Language language)
    {
        _host = host;
        _inputScanner = inputScanner;
        Loc.SetLanguage(language);
        _enableEngineFallback = _host.EngineFallbackPreference;

        AddFilesCommand = new RelayCommand(AddFiles, () => !_host.IsRunActive);
        AddFolderCommand = new RelayCommand(AddFolder, () => !_host.IsRunActive);
        RemoveSelectedCommand = new RelayCommand(
            RemoveSelected,
            () => !_host.IsRunActive && SelectedQueueItem is not null);
        ClearCommand = new RelayCommand(ClearQueue, () => !_host.IsRunActive && Queue.Count > 0);
        ChooseOutputFolderCommand = new RelayCommand(ChooseOutputFolder, () => !_host.IsRunActive);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !_host.IsRunActive);
        CancelCommand = new RelayCommand(Cancel, () => _host.IsRunActive);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        OpenItemOutputCommand = new RelayCommand<QueueItemViewModel>(
            OpenItemOutput,
            item => !_host.IsRunActive && item is not null && !string.IsNullOrWhiteSpace(item.Output));

        RefreshChoices();
        RefreshSelectedEngineGuidance();

        // Lifecycle note: the shell and all pages live for the whole app session (single
        // window), so these subscriptions never outlive their owner. Detach() exists so the
        // page is still leak-free if a future shell recreates pages.
        _host.EngineAvailabilityChanged += OnEngineAvailabilityChanged;
        _host.RunStateChanged += OnRunStateChanged;
    }

    public LocalizationService Loc { get; } = new();

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    public ObservableCollection<ChoiceItem<BatchConversionTarget>> TargetChoices { get; } = new();

    public ObservableCollection<ChoiceItem<BatchConversionEnginePreference>> EngineChoices { get; } = new();

    public ObservableCollection<ChoiceItem<ImageOutputFormat>> ImageFormatChoices { get; } = new();

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand ChooseOutputFolderCommand { get; }

    public AsyncRelayCommand StartCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand OpenOutputFolderCommand { get; }

    public RelayCommand<QueueItemViewModel> OpenItemOutputCommand { get; }

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

    public bool EnableEngineFallback
    {
        get => _enableEngineFallback;
        set
        {
            if (SetProperty(ref _enableEngineFallback, value))
            {
                _host.PersistEngineFallbackPreference(value);
                Log($"Auto engine fallback set: {value}.");
            }
        }
    }

    public string SelectedEngineGuidance
    {
        get => _selectedEngineGuidance;
        set => SetProperty(ref _selectedEngineGuidance, value);
    }

    public double OverallPercent
    {
        get => _overallPercent;
        private set => SetProperty(ref _overallPercent, value);
    }

    // Pure computation exposed for regression tests: file-granularity overall progress, 0-100.
    // A Running report means the file has NOT completed; any terminal report counts it as
    // done. Items cancelled after a break are never reported by the pipeline, so the value
    // simply stays below 100 until the next batch resets it.
    public static double ComputeOverallPercent(
        BatchConversionItemStatus status,
        int currentItemIndex,
        int totalItems)
    {
        if (totalItems <= 0)
        {
            return 0;
        }

        var completed = status == BatchConversionItemStatus.Running
            ? currentItemIndex
            : currentItemIndex + 1;
        return Math.Clamp(completed * 100.0 / totalItems, 0, 100);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        if (_host.IsRunActive)
        {
            ShowAlert(Loc["WaitCurrentTask"]);
            Log("Add blocked: task running.");
            return;
        }

        // Drop paths that are already queued with a log line only: drag-drop can reach this
        // method through routed events from both the drop zone and the window, and re-adding
        // the same paths must never pollute the rejected list or raise a false alert.
        // The queue set doubles as the "seen" filter for the incoming payload itself, but it
        // is never used to reject scanned results below: once a path survives the filter it
        // is genuinely new (regression caught by the real-drag probe: merging the incoming
        // paths into the queue set first made every accepted file look already queued, and
        // drops kept producing "0 file(s) queued." with no rows and no alert).
        var seen = Queue
            .Select(item => item.InputPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incoming = new List<string>();
        foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var fullPath = Path.GetFullPath(rawPath);
            if (seen.Add(fullPath))
            {
                incoming.Add(fullPath);
            }
            else
            {
                Log($"Duplicate re-add ignored: {fullPath}");
            }
        }

        // `incoming` is already free of queue duplicates and of duplicates inside one drop
        // payload, so every scanned-accepted path is a genuinely new queue entry.
        var scan = _inputScanner.Scan(incoming);
        var added = 0;

        foreach (var filePath in scan.AcceptedFiles)
        {
            Queue.Add(new QueueItemViewModel(filePath, SelectedTarget, SelectedEngine));
            added++;
        }

        foreach (var rejectedPath in scan.RejectedPaths.Take(20))
        {
            _host.AddRejectedPath(rejectedPath, "Unsupported or missing");
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
            _host.ClearAlert();
        }

        _host.SetSummaryText($"{Queue.Count} file(s) queued.");
        Log($"Added {added} file(s).");
        RaiseCommandStates();
    }

    private void AddFiles() => AddPaths(_host.PickInputFiles());

    private void AddFolder()
    {
        var folder = _host.PickFolder("Add folder");
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
        _host.SetSummaryText($"{Queue.Count} file(s) queued.");
        RaiseCommandStates();
    }

    private void ClearQueue()
    {
        Queue.Clear();
        _host.ClearRejectedPaths();
        _host.SetSummaryText("Queue cleared.");
        Log("Queue cleared.");
        RaiseCommandStates();
    }

    private void ChooseOutputFolder()
    {
        var folder = _host.PickFolder("Select output folder");
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
            _host.SetSummaryText(Loc["AddFilesBeforeStart"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            ShowAlert(Loc["ChooseOutputBeforeStart"]);
            Log("Start blocked: output folder is required.");
            _host.SetSummaryText(Loc["ChooseOutputBeforeStart"]);
            return;
        }

        if (Dpi is < 72 or > 600)
        {
            ShowAlert(Loc["DpiInvalid"]);
            Log("Start blocked: DPI must be 72-600.");
            _host.SetSummaryText(Loc["DpiInvalid"]);
            return;
        }

        var lease = _host.TryBeginRun();
        if (lease is null)
        {
            // The command's CanExecute already gates this; kept for robustness with the
            // same wait message the add entry points use.
            ShowAlert(Loc["WaitCurrentTask"]);
            Log("Start blocked: task running.");
            return;
        }

        // Disposing the lease ends the run for both models: raises the run-state change,
        // disposes the cancellation source and clears it — identical to the previous
        // inline finally block.
        using (lease)
        {
            _host.SetSummaryText(Loc["Running"]);
            OverallPercent = 0;

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
                _host.ClearAlert();

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
                        EnableEngineFallback = EnableEngineFallback,
                        LibreOfficeExecutablePath = _host.CustomLibreOfficePath,
                        OverwritePolicy = OverwritePolicy.AutoRename
                    });

                var token = lease.Token;
                var progress = new Progress<BatchConversionProgress>(UpdateProgress);
                var batchRunner = _host.CreateBatchRunner();
                var result = await Task.Run(
                        () => batchRunner.RunAsync(job, progress, token),
                        token)
                    .ConfigureAwait(true);

                foreach (var itemResult in result.Items)
                {
                    ApplyResult(itemResult);
                }

                var summary =
                    $"Done. OK {result.SucceededCount}, failed {result.FailedCount}, cancelled {result.CancelledCount}.";
                _host.SetSummaryText(summary);
                Log(summary);
            }
            catch (OperationCanceledException)
            {
                _host.SetSummaryText("Batch cancelled.");
                Log("Batch cancelled.");
            }
            catch (Exception ex)
            {
                _host.SetSummaryText("Batch failed.");
                ShowAlert(ex.Message);
                Log($"Batch error: {ex.Message}");
            }
        }
    }

    private void Cancel()
    {
        _host.RequestCancelRun();
        _host.SetSummaryText("Cancel requested.");
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

    private void OpenItemOutput(QueueItemViewModel? item)    {
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
            Process.Start(new ProcessStartInfo(
                "explorer.exe",
                $"/select,\"{firstPath}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        Log($"Open item output blocked: path not found for {item.FileName}.");
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
        _host.SetSummaryText($"{progress.CurrentItemIndex + 1}/{progress.TotalItems}: {item.FileName}");

        // Honest file-level percent: Running means the file has not finished yet, so only
        // terminal reports advance the completed count. (Pipeline raises one Running and one
        // terminal report per item; the payload's own PercentComplete is index-based and
        // overcounts while a file is still running, which is why it is not read directly.)
        OverallPercent = ComputeOverallPercent(
            progress.Status,
            progress.CurrentItemIndex,
            progress.TotalItems);
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

        if (result.Fallback is { } fallback)
        {
            var fileName = item.FileName;
            var primaryEngineName = EngineName(fallback.PrimaryEngine);
            var fallbackEngineName = EngineName(fallback.FallbackEngine);

            Log(string.Format(
                Loc["FallbackAttempt"],
                fileName,
                primaryEngineName,
                fallback.PrimaryFailureReason,
                fallbackEngineName));

            Log(fallback.FallbackSucceeded
                ? string.Format(Loc["FallbackSuccess"], fileName, fallbackEngineName)
                : string.Format(Loc["BothEnginesFailed"], fileName));
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Log($"{item.FileName}: {result.ErrorMessage}");
        }
    }

    private string EngineName(ConversionEngineKind engine) => engine switch
    {
        ConversionEngineKind.MicrosoftOffice => Loc["EngineMicrosoftOffice"],
        ConversionEngineKind.LibreOffice => Loc["EngineLibreOffice"],
        _ => engine.ToString()
    };

    private void RefreshSelectedEngineGuidance() => SelectedEngineGuidance = _host.GetEngineGuidance(SelectedEngine);

    internal void OnShellLanguageChanged(Language language)
    {
        Loc.SetLanguage(language);
        RefreshChoices();
        RefreshSelectedEngineGuidance();
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

    private void ShowAlert(string message)
    {
        _host.ShowAlert(message);
    }

    private void Log(string message)
    {
        _host.Log(message);
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
        OpenItemOutputCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Detaches shell event subscriptions (shells and pages both live app-long).</summary>
    public void Detach()
    {
        _host.EngineAvailabilityChanged -= OnEngineAvailabilityChanged;
        _host.RunStateChanged -= OnRunStateChanged;
    }

    private void OnEngineAvailabilityChanged(object? sender, EventArgs e) => RefreshSelectedEngineGuidance();

    private void OnRunStateChanged(object? sender, EventArgs e) => RaiseCommandStates();
}

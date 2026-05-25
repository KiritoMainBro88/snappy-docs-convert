using System.IO;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.App.ViewModels;

public sealed class QueueItemViewModel : ObservableObject
{
    private string _target;
    private string _engine;
    private string _status = "Pending";
    private string _message = "";
    private string _output = "";

    public QueueItemViewModel(
        string inputPath,
        BatchConversionTarget target,
        BatchConversionEnginePreference engine)
    {
        InputPath = inputPath;
        FileName = Path.GetFileName(inputPath);
        Type = Path.GetExtension(inputPath).TrimStart('.').ToUpperInvariant();
        _target = target.ToString();
        _engine = engine.ToString();
    }

    public string InputPath { get; }

    public string FileName { get; }

    public string Type { get; }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string Engine
    {
        get => _engine;
        set => SetProperty(ref _engine, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string Output
    {
        get => _output;
        set => SetProperty(ref _output, value);
    }

    public void ApplySettings(
        BatchConversionTarget target,
        BatchConversionEnginePreference engine)
    {
        Target = target.ToString();
        Engine = engine.ToString();
    }
}

namespace SnappyDocsConvert.App.ViewModels;

public sealed class EngineStatusViewModel : ObservableObject
{
    private string _wordStatus = "Checking";
    private string _powerPointStatus = "Checking";
    private string _libreOfficeStatus = "Checking";
    private string _pdfRendererStatus = "Available";
    private string _libreOfficePath = "";
    private string _guidance = "Recheck engines before running document conversions.";

    public string WordStatus
    {
        get => _wordStatus;
        set => SetProperty(ref _wordStatus, value);
    }

    public string PowerPointStatus
    {
        get => _powerPointStatus;
        set => SetProperty(ref _powerPointStatus, value);
    }

    public string LibreOfficeStatus
    {
        get => _libreOfficeStatus;
        set => SetProperty(ref _libreOfficeStatus, value);
    }

    public string PdfRendererStatus
    {
        get => _pdfRendererStatus;
        set => SetProperty(ref _pdfRendererStatus, value);
    }

    public string LibreOfficePath
    {
        get => _libreOfficePath;
        set => SetProperty(ref _libreOfficePath, value);
    }

    public string Guidance
    {
        get => _guidance;
        set => SetProperty(ref _guidance, value);
    }
}

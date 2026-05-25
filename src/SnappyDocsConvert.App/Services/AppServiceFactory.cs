using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.LibreOffice;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Pdf;
using SnappyDocsConvert.Core.Services.PdfTools;
using SnappyDocsConvert.Core.Services.Updates;

namespace SnappyDocsConvert.App.Services;

public sealed class AppServiceFactory
{
    public AppSettingsService CreateAppSettingsService() => new();

    public ThemeService CreateThemeService() => new();

    public AppUpdateService CreateAppUpdateService() => new();

    public FilePickerService CreateFilePickerService() => new();

    public FolderPickerService CreateFolderPickerService() => new();

    public IBatchInputScanner CreateBatchInputScanner() => new BatchInputScanner();

    public IMicrosoftOfficeAvailabilityProvider CreateOfficeAvailabilityProvider()
        => new MicrosoftOfficeAvailabilityProvider();

    public ILibreOfficeLocator CreateLibreOfficeLocator()
        => new LibreOfficeLocator();

    public IEngineSetupAdvisor CreateEngineSetupAdvisor()
        => new EngineSetupAdvisor(CreateLibreOfficeLocator(), CreateOfficeAvailabilityProvider());

    public UiBatchRunner CreateBatchRunner()
        => new(new BatchConversionPipeline(
            new BatchOutputPlanner(),
            new ConversionEngineSelector(
                CreateOfficeAvailabilityProvider(),
                CreateLibreOfficeLocator()),
            new LibreOfficeConversionEngine(),
            new OfficeComConversionEngine(),
            new PdfToImageRenderer()));

    public IPdfToolService CreatePdfToolService() => new PdfToolService();
}

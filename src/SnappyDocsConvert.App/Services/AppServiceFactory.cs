using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.LibreOffice;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Pdf;

namespace SnappyDocsConvert.App.Services;

public sealed class AppServiceFactory
{
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
}

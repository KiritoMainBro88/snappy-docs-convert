using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.LibreOffice;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Ocr;
using SnappyDocsConvert.Core.Services.Paths;
using SnappyDocsConvert.Core.Services.Pdf;
using SnappyDocsConvert.Core.Services.PdfTools;
using SnappyDocsConvert.Core.Services.Process;
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
    {
        // One Office session per batch: the SessionOfficeComRunner keeps a single Word/PowerPoint
        // instance alive across all files (reused, not reopened per file) and is disposed when the
        // batch finishes. Falls back to LibreOffice exactly as before on recoverable failures.
        var officeRunner = new SessionOfficeComRunner();
        var pipeline = new BatchConversionPipeline(
            new BatchOutputPlanner(),
            new ConversionEngineSelector(
                CreateOfficeAvailabilityProvider(),
                CreateLibreOfficeLocator()),
            new LibreOfficeConversionEngine(),
            new OfficeComConversionEngine(
                CreateOfficeAvailabilityProvider(),
                officeRunner,
                new PathService(),
                new OfficeConversionOptions()),
            new PdfToImageRenderer());

        return new UiBatchRunner(pipeline, officeRunner);
    }

    public IPdfToolService CreatePdfToolService() => new PdfToolService();

    public PdfToDocumentConverter CreatePdfToDocumentConverter() => new();

    public OcrDocumentService? CreateOcrService(string? paddleOcrPath)
    {
        if (string.IsNullOrWhiteSpace(paddleOcrPath) || !System.IO.File.Exists(paddleOcrPath))
        {
            return null;
        }

        var engine = new PaddleOcrEngine(new ProcessRunner(), paddleOcrPath);
        return new OcrDocumentService(engine, new PdfToImageRenderer());
    }
}

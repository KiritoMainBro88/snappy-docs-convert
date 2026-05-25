using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

public interface IOfficeComRunner
{
    Task<OfficeComExportResult> ExportToPdfAsync(
        OfficeAppKind appKind,
        string inputPath,
        string outputPdfPath,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

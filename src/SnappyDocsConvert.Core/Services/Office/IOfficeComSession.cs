using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// A per-batch Office automation session. Keeps at most one Application instance per app-kind alive
/// across multiple files (lazy created on first use), then quits/releases everything on disposal.
/// </summary>
public interface IOfficeComSession : IDisposable
{
    Task<OfficeComExportResult> ExportToPdfAsync(
        OfficeAppKind appKind,
        string inputPath,
        string outputPdfPath,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reverse conversion through a reused app instance: opens a PDF in the given app-kind (Word) and
    /// saves it as an editable document at <paramref name="outputPath"/>.
    /// </summary>
    Task<OfficeComExportResult> ConvertFromPdfAsync(
        OfficeAppKind appKind,
        string inputPdfPath,
        string outputPath,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>Number of files exported through a reused Application, per app-kind (for logging Req 5.1).</summary>
    int GetReuseCount(OfficeAppKind appKind);
}

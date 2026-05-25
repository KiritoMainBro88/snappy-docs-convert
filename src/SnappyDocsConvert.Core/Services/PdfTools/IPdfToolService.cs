using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.PdfTools;

public interface IPdfToolService
{
    Task<PdfToolResult> RunAsync(
        PdfToolRequest request,
        CancellationToken cancellationToken);
}

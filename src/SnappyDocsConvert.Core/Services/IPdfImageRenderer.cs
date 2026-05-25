using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface IPdfImageRenderer
{
    Task<PdfRenderResult> RenderAsync(
        PdfRenderRequest request,
        CancellationToken cancellationToken);
}

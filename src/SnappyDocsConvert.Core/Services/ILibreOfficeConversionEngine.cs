using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services;

public interface ILibreOfficeConversionEngine
{
    Task<ConversionResult> ConvertToPdfAsync(
        ConversionRequest request,
        CancellationToken cancellationToken);
}

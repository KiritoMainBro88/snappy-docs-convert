using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

public interface IOfficeComConversionEngine
{
    Task<ConversionResult> ConvertToPdfAsync(
        ConversionRequest request,
        CancellationToken cancellationToken);
}

using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public interface IConversionEngineSelector
{
    Task<BatchEngineSelectionResult> SelectAsync(
        string inputPath,
        BatchConversionOptions options,
        CancellationToken cancellationToken);
}

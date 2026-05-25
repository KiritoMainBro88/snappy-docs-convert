using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public interface IBatchConversionPipeline
{
    Task<BatchConversionResult> RunAsync(
        BatchConversionJob job,
        IProgress<BatchConversionProgress>? progress,
        CancellationToken cancellationToken);
}

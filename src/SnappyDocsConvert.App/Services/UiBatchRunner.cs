using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.App.Services;

public sealed class UiBatchRunner
{
    private readonly IBatchConversionPipeline _pipeline;

    public UiBatchRunner(IBatchConversionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<BatchConversionResult> RunAsync(
        BatchConversionJob job,
        IProgress<BatchConversionProgress>? progress,
        CancellationToken cancellationToken)
        => _pipeline.RunAsync(job, progress, cancellationToken);
}

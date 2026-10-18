using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.App.Services;

public sealed class UiBatchRunner
{
    private readonly IBatchConversionPipeline _pipeline;
    private readonly IDisposable? _officeRunner;

    public UiBatchRunner(IBatchConversionPipeline pipeline, IDisposable? officeRunner = null)
    {
        _pipeline = pipeline;
        _officeRunner = officeRunner;
    }

    public async Task<BatchConversionResult> RunAsync(
        BatchConversionJob job,
        IProgress<BatchConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.RunAsync(job, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Quit/release the reused Office instance(s) once the batch is done so no Office process
            // is left running. A new runner is created per batch by AppServiceFactory.
            _officeRunner?.Dispose();
        }
    }
}

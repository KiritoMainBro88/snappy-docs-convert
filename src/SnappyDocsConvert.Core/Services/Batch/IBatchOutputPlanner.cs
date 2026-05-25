using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public interface IBatchOutputPlanner
{
    BatchOutputPlan Plan(BatchConversionJob job);

    BatchOutputItemPlan PlanItem(
        BatchConversionItem item,
        BatchConversionOptions options);
}

using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Batch;

public interface IBatchInputScanner
{
    BatchInputScanResult Scan(IEnumerable<string> paths);
}

namespace SnappyDocsConvert.Core.Models;

public sealed record BatchInputScanResult(
    IReadOnlyList<string> AcceptedFiles,
    IReadOnlyList<string> RejectedPaths);

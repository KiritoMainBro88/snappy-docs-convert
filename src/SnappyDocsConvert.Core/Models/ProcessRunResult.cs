namespace SnappyDocsConvert.Core.Models;

public sealed record ProcessRunResult(
    int? ExitCode,
    bool TimedOut,
    string Stdout,
    string Stderr,
    TimeSpan Duration);

namespace SnappyDocsConvert.Core.Models;

public sealed record BatchOutputItemPlan
{
    public required string InputPath { get; init; }

    public required string OutputRoot { get; init; }

    public required string SafeBaseName { get; init; }

    public required string Hash8 { get; init; }

    public required string PdfOutputPath { get; init; }

    public required string ImageOutputDirectory { get; init; }

    public required string PagePrefix { get; init; }
}

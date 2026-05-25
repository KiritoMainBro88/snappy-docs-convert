namespace SnappyDocsConvert.Core.Models;

public sealed record BatchConversionOptions
{
    public required string OutputRoot { get; init; }

    public BatchConversionTarget Target { get; init; } = BatchConversionTarget.PdfAndImages;

    public BatchConversionEnginePreference EnginePreference { get; init; } = BatchConversionEnginePreference.Auto;

    public ImageOutputFormat ImageFormat { get; init; } = ImageOutputFormat.Png;

    public int Dpi { get; init; } = 200;

    public int JpegQuality { get; init; } = 90;

    public bool KeepIntermediatePdf { get; init; }

    public OverwritePolicy OverwritePolicy { get; init; } = OverwritePolicy.AutoRename;

    public int MaxDocumentConversions { get; init; } = 1;

    public int MaxImageRenderJobs { get; init; } = 1;

    public IntermediatePdfPolicy IntermediatePdfPolicy =>
        KeepIntermediatePdf ? IntermediatePdfPolicy.Keep : IntermediatePdfPolicy.DeleteWhenImagesOnly;
}

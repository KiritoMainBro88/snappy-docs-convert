namespace SnappyDocsConvert.Core.Models;

public sealed record PdfRenderOptions
{
    public int Dpi { get; init; } = 200;

    public ImageOutputFormat Format { get; init; } = ImageOutputFormat.Png;

    public int JpegQuality { get; init; } = 90;

    public OverwritePolicy OverwritePolicy { get; init; } = OverwritePolicy.AutoRename;

    public string PagePrefix { get; init; } = "page";

    public int? StartPage { get; init; }

    public int? EndPage { get; init; }
}

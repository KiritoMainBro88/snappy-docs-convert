using System.Text;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Pdf;

namespace SnappyDocsConvert.Tests;

public sealed class PdfImageRendererTests
{
    [Fact]
    public void PdfRenderOptions_DefaultsMatchPhase4Contract()
    {
        var options = new PdfRenderOptions();

        Assert.Equal(200, options.Dpi);
        Assert.Equal(ImageOutputFormat.Png, options.Format);
        Assert.Equal(90, options.JpegQuality);
        Assert.Equal(OverwritePolicy.AutoRename, options.OverwritePolicy);
        Assert.Equal("page", options.PagePrefix);
    }

    [Fact]
    public async Task RenderAsync_ReturnsFailure_ForMissingPdf()
    {
        using var temp = TempDirectory.Create();
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            new PdfRenderRequest(Path.Combine(temp.Path, "missing.pdf"), temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_ReturnsFailure_ForNonPdfInput()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.txt", "not a pdf");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            new PdfRenderRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(".pdf", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_CreatesOutputDirectory()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = Path.Combine(temp.Path, "created-output");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(Directory.Exists(outputDirectory));
    }

    [Fact]
    public async Task RenderAsync_RendersPng_FromTinyPdf()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions { Format = ImageOutputFormat.Png }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var outputFile = Assert.Single(result.OutputFiles);
        Assert.EndsWith(".png", outputFile, StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(outputFile).Length > 0);
    }

    [Fact]
    public async Task RenderAsync_RendersJpeg_FromTinyPdf()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions
            {
                Format = ImageOutputFormat.Jpeg,
                JpegQuality = 85
            }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var outputFile = Assert.Single(result.OutputFiles);
        Assert.EndsWith(".jpg", outputFile, StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(outputFile).Length > 0);
    }

    [Fact]
    public async Task RenderAsync_RendersWebp_OrReturnsClearFailure()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions
            {
                Format = ImageOutputFormat.Webp,
                JpegQuality = 85
            }),
            CancellationToken.None);

        if (result.Success)
        {
            var outputFile = Assert.Single(result.OutputFiles);
            Assert.EndsWith(".webp", outputFile, StringComparison.OrdinalIgnoreCase);
            Assert.True(new FileInfo(outputFile).Length > 0);
        }
        else
        {
            Assert.Contains("WebP", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RenderAsync_UsesDefaultPageFileName()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("page-001.png", Path.GetFileName(Assert.Single(result.OutputFiles)));
    }

    [Fact]
    public async Task RenderAsync_UsesCustomPagePrefix()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions { PagePrefix = "slide" }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("slide-001.png", Path.GetFileName(Assert.Single(result.OutputFiles)));
    }

    [Fact]
    public async Task RenderAsync_AutoRenameAvoidsOverwrite()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var existingPath = Path.Combine(outputDirectory, "page-001.png");
        File.WriteAllText(existingPath, "existing");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions { OverwritePolicy = OverwritePolicy.AutoRename }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("existing", File.ReadAllText(existingPath));
        Assert.Equal("page-001 (1).png", Path.GetFileName(Assert.Single(result.OutputFiles)));
    }

    [Fact]
    public async Task RenderAsync_OverwriteReplacesExistingFile()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var existingPath = Path.Combine(outputDirectory, "page-001.png");
        File.WriteAllText(existingPath, "old");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions { OverwritePolicy = OverwritePolicy.Overwrite }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(existingPath, Assert.Single(result.OutputFiles));
        Assert.NotEqual("old", File.ReadAllText(existingPath));
    }

    [Fact]
    public async Task RenderAsync_SkipLeavesExistingFileAndWarns()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var outputDirectory = temp.CreateSubdirectory("out");
        var existingPath = Path.Combine(outputDirectory, "page-001.png");
        File.WriteAllText(existingPath, "old");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, outputDirectory, new PdfRenderOptions { OverwritePolicy = OverwritePolicy.Skip }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(0, result.PagesRendered);
        Assert.Equal("old", File.ReadAllText(existingPath));
        Assert.Contains(result.Warnings, warning => warning.Contains("Skipped", StringComparison.OrdinalIgnoreCase));
        Assert.True(Assert.Single(result.PageResults).Skipped);
    }

    [Theory]
    [InlineData(71)]
    [InlineData(601)]
    public async Task RenderAsync_RejectsInvalidDpi(int dpi)
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, temp.CreateSubdirectory("out"), new PdfRenderOptions { Dpi = dpi }),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("DPI", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_AddsWarning_ForHighDpi()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, temp.CreateSubdirectory("out"), new PdfRenderOptions { Dpi = 301 }),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(result.Warnings, warning => warning.Contains("High DPI", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task RenderAsync_RejectsInvalidJpegQuality(int jpegQuality)
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, temp.CreateSubdirectory("out"), new PdfRenderOptions { JpegQuality = jpegQuality }),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("JPEG quality", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_ReturnsFailure_WhenCancelledBeforeRender()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var renderer = new PdfToImageRenderer();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await renderer.RenderAsync(
            Request(inputPath, temp.CreateSubdirectory("out")),
            cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAsync_ResultIncludesOutputFilesAndPageCount()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteTinyPdf("input.pdf");
        var renderer = new PdfToImageRenderer();

        var result = await renderer.RenderAsync(
            Request(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.PagesRendered);
        Assert.Single(result.OutputFiles);
        Assert.Single(result.PageResults);
    }

    private static PdfRenderRequest Request(
        string inputPath,
        string outputDirectory,
        PdfRenderOptions? options = null)
        => new(inputPath, outputDirectory)
        {
            Options = options is null ? new PdfRenderOptions { Dpi = 72 } : options with { Dpi = options.Dpi }
        };

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SnappyDocsConvertPdfTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public string CreateSubdirectory(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public string WriteFile(string fileName, string content)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public string WriteTinyPdf(string fileName)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllBytes(path, TinyPdf.Create());
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static class TinyPdf
    {
        public static byte[] Create()
        {
            const string pageText = "BT /F1 18 Tf 36 90 Td (Snappy Docs Convert) Tj ET\n";
            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 140] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(pageText)} >>\nstream\n{pageText}endstream"
            };

            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.4\n");
            var offsets = new List<long>();

            for (var index = 0; index < objects.Length; index++)
            {
                offsets.Add(stream.Position);
                WriteAscii(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            }

            var xrefOffset = stream.Position;
            WriteAscii(stream, $"xref\n0 {objects.Length + 1}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            foreach (var offset in offsets)
            {
                WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
            WriteAscii(stream, $"startxref\n{xrefOffset}\n%%EOF\n");
            return stream.ToArray();
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}

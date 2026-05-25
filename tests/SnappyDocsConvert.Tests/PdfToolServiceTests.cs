using PdfSharp.Pdf;
using SkiaSharp;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.PdfTools;

namespace SnappyDocsConvert.Tests;

public sealed class PdfToolServiceTests
{
    [Fact]
    public void PageRangeParser_ParsesSinglePage()
    {
        var result = new PageRangeParser().Parse("1");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { 1 }, result.Pages);
    }

    [Fact]
    public void PageRangeParser_ParsesRangesAndRemovesDuplicates()
    {
        var result = new PageRangeParser().Parse("1,3-5,3");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { 1, 3, 4, 5 }, result.Pages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("5-3")]
    [InlineData("abc")]
    [InlineData("1-")]
    public void PageRangeParser_InvalidRangesFailClearly(string value)
    {
        var result = new PageRangeParser().Parse(value);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Merge_TwoGeneratedPdfs_ProducesOutput()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WritePdf("one.pdf", 1);
        var second = temp.WritePdf("two.pdf", 1);
        var output = temp.CreateSubdirectory("out");

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Merge,
            InputPaths = new[] { first, second },
            OutputDirectory = output,
            OutputFileName = "merged.pdf"
        }, CancellationToken.None);

        AssertOutput(result, expectedCount: 1);
    }

    [Fact]
    public async Task Split_MultiPagePdf_ProducesOnePdfPerPage()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WritePdf("multi.pdf", 2);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Split,
            InputPaths = new[] { input },
            OutputDirectory = temp.CreateSubdirectory("out")
        }, CancellationToken.None);

        AssertOutput(result, expectedCount: 2);
    }

    [Fact]
    public async Task Extract_PageOne_ProducesOutput()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WritePdf("multi.pdf", 2);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.ExtractPages,
            InputPaths = new[] { input },
            OutputDirectory = temp.CreateSubdirectory("out"),
            PageRanges = "1"
        }, CancellationToken.None);

        AssertOutput(result, expectedCount: 1);
    }

    [Fact]
    public async Task Rotate_Page_ProducesOutput()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WritePdf("multi.pdf", 2);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.RotatePages,
            InputPaths = new[] { input },
            OutputDirectory = temp.CreateSubdirectory("out"),
            PageRanges = "1",
            RotationAngle = PdfRotationAngle.Degrees90
        }, CancellationToken.None);

        AssertOutput(result, expectedCount: 1);
    }

    [Fact]
    public async Task ImagesToPdf_GeneratedPngAndJpeg_ProducesOutput()
    {
        using var temp = TempDirectory.Create();
        var png = temp.WriteImage("one.png", SKEncodedImageFormat.Png);
        var jpeg = temp.WriteImage("two.jpg", SKEncodedImageFormat.Jpeg);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.ImagesToPdf,
            InputPaths = new[] { png, jpeg },
            OutputDirectory = temp.CreateSubdirectory("out"),
            OutputFileName = "images.pdf"
        }, CancellationToken.None);

        AssertOutput(result, expectedCount: 1);
    }

    [Fact]
    public async Task MissingInput_Fails()
    {
        using var temp = TempDirectory.Create();

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Merge,
            InputPaths = new[] { Path.Combine(temp.Path, "missing.pdf") },
            OutputDirectory = temp.CreateSubdirectory("out")
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonPdfInput_ForPdfOperation_Fails()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WriteFile("bad.txt", "not pdf");

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Split,
            InputPaths = new[] { input },
            OutputDirectory = temp.CreateSubdirectory("out")
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(".pdf", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SourceOverwrite_Blocked()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WritePdf("source.pdf", 1);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.RotatePages,
            InputPaths = new[] { input },
            OutputDirectory = temp.Path,
            OutputFileName = "source.pdf"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("overwrite", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutputPathTraversal_Blocked()
    {
        using var temp = TempDirectory.Create();
        var input = temp.WritePdf("source.pdf", 1);

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.RotatePages,
            InputPaths = new[] { input },
            OutputDirectory = temp.CreateSubdirectory("out"),
            OutputFileName = "..\\evil.pdf"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("path", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static PdfToolService Service() => new();

    private static void AssertOutput(PdfToolResult result, int expectedCount)
    {
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(expectedCount, result.OutputFiles.Count);
        foreach (var outputFile in result.OutputFiles)
        {
            Assert.True(File.Exists(outputFile), outputFile);
            Assert.True(new FileInfo(outputFile).Length > 0, outputFile);
        }
    }

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
                "SnappyDocsConvertPdfToolTests",
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

        public string WritePdf(string fileName, int pageCount)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            using var document = new PdfDocument();
            for (var index = 0; index < pageCount; index++)
            {
                document.AddPage();
            }

            document.Save(path);
            return path;
        }

        public string WriteImage(string fileName, SKEncodedImageFormat format)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            using var bitmap = new SKBitmap(24, 24);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.RoyalBlue };
            canvas.DrawRect(4, 4, 16, 16, paint);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 90);
            using var stream = File.Create(path);
            data.SaveTo(stream);
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
}

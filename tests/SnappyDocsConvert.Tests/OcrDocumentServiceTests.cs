using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Ocr;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Tests for <see cref="OcrDocumentService"/> orchestration using fake OCR engine + fake renderer.
/// No PaddleOCR installed: the engine fake stands in for it. Verifies image path, PDF page fan-out,
/// engine-unavailable handling, and text file output.
/// </summary>
public sealed class OcrDocumentServiceTests
{
    [Fact]
    public async Task Image_WritesRecognisedTextToTxt()
    {
        using var temp = TempDirectory.Create();
        var image = temp.WriteFile("scan.png", "fake png bytes");
        var outDir = temp.CreateSubdirectory("out");
        var service = new OcrDocumentService(
            new FakeOcrEngine(available: true, textPerImage: _ => "hello world"),
            new FakeRenderer());

        var result = await service.RecognizeAsync(image, outDir, 200, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello world", result.Text);
        Assert.True(File.Exists(result.OutputTextPath));
        Assert.Equal("hello world", File.ReadAllText(result.OutputTextPath));
    }

    [Fact]
    public async Task EngineUnavailable_Fails()
    {
        using var temp = TempDirectory.Create();
        var image = temp.WriteFile("scan.png", "x");
        var service = new OcrDocumentService(
            new FakeOcrEngine(available: false, textPerImage: _ => ""),
            new FakeRenderer());

        var result = await service.RecognizeAsync(image, temp.CreateSubdirectory("out"), 200, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("OCR engine", result.ErrorMessage);
    }

    [Fact]
    public async Task Pdf_RastersisesPagesAndConcatenatesText()
    {
        using var temp = TempDirectory.Create();
        var pdf = temp.WriteFile("scan.pdf", "%PDF");
        var outDir = temp.CreateSubdirectory("out");

        // Fake renderer yields 3 page images; engine returns the file name as its text so we can
        // assert all pages were OCR'd and concatenated in order.
        var renderer = new FakeRenderer(pageCount: 3);
        var service = new OcrDocumentService(
            new FakeOcrEngine(available: true, textPerImage: Path.GetFileNameWithoutExtension),
            renderer);

        var result = await service.RecognizeAsync(pdf, outDir, 150, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("page-001", result.Text);
        Assert.Contains("page-002", result.Text);
        Assert.Contains("page-003", result.Text);
        Assert.True(File.Exists(result.OutputTextPath));
    }

    [Fact]
    public async Task UnsupportedExtension_Fails()
    {
        using var temp = TempDirectory.Create();
        var file = temp.WriteFile("data.bin", "x");
        var service = new OcrDocumentService(
            new FakeOcrEngine(available: true, textPerImage: _ => "t"),
            new FakeRenderer());

        var result = await service.RecognizeAsync(file, temp.CreateSubdirectory("out"), 200, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Image_NameCollision_DoesNotOverwriteExistingTxt()
    {
        using var temp = TempDirectory.Create();
        var image = temp.WriteFile("scan.png", "fake png bytes");
        var outDir = temp.CreateSubdirectory("out");
        var existing = Path.Combine(outDir, "scan.txt");
        await File.WriteAllTextAsync(existing, "earlier ocr text");

        var service = new OcrDocumentService(
            new FakeOcrEngine(available: true, textPerImage: _ => "new text"),
            new FakeRenderer());
        var result = await service.RecognizeAsync(image, outDir, 200, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("new text", result.Text);
        Assert.Matches(@"^scan__[0-9A-F]{8}\.txt$", Path.GetFileName(result.OutputTextPath));

        // The pre-existing scan.txt is untouched: no silent overwrite.
        Assert.Equal("earlier ocr text", await File.ReadAllTextAsync(existing));
        Assert.Equal("new text", await File.ReadAllTextAsync(result.OutputTextPath));
    }

    [Fact]
    public async Task Image_RepeatedRunsOnSameInput_NeverOverwritesEachOther()
    {
        using var temp = TempDirectory.Create();
        var image = temp.WriteFile("scan.png", "fake png bytes");
        var outDir = temp.CreateSubdirectory("out");
        var service = new OcrDocumentService(
            new FakeOcrEngine(available: true, textPerImage: _ => $"run text"),
            new FakeRenderer());

        var first = await service.RecognizeAsync(image, outDir, 200, CancellationToken.None);
        var second = await service.RecognizeAsync(image, outDir, 200, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.OutputTextPath, second.OutputTextPath);
        Assert.Equal(first.OutputTextPath, Path.Combine(outDir, "scan.txt"));
        Assert.Matches(@"^scan__[0-9A-F]{8}\.txt$", Path.GetFileName(second.OutputTextPath));
    }

    private sealed class FakeOcrEngine : IOcrEngine
    {
        private readonly bool _available;
        private readonly Func<string, string> _textPerImage;

        public FakeOcrEngine(bool available, Func<string, string> textPerImage)
        {
            _available = available;
            _textPerImage = textPerImage;
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
            => Task.FromResult(_available);

        public Task<OcrResult> RecognizeImageAsync(string imagePath, CancellationToken cancellationToken)
            => Task.FromResult(OcrResult.Ok(_textPerImage(imagePath)));
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SnappyDocsConvertOcrTests",
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

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FakeRenderer : IPdfImageRenderer
    {
        private readonly int _pageCount;

        public FakeRenderer(int pageCount = 1)
        {
            _pageCount = pageCount;
        }

        public Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(request.OutputDirectory);
            var files = new List<string>();
            for (var i = 1; i <= _pageCount; i++)
            {
                var path = Path.Combine(request.OutputDirectory, $"page-{i:D3}.png");
                File.WriteAllText(path, "img");
                files.Add(path);
            }

            return Task.FromResult(new PdfRenderResult
            {
                Success = true,
                InputPdfPath = request.InputPdfPath,
                OutputDirectory = request.OutputDirectory,
                PagesRendered = _pageCount,
                OutputFiles = files
            });
        }
    }
}

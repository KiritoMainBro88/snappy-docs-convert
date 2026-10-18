using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Ocr;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Tests for <see cref="PaddleOcrEngine"/> command build + stdout parsing, with a fake process runner
/// (no PaddleOCR installed). Engine availability is path-based and also covered.
/// </summary>
public sealed class PaddleOcrEngineTests
{
    [Fact]
    public void ParseRecognizedText_ExtractsQuotedTextFromTuples()
    {
        var stdout = string.Join('\n', new[]
        {
            "[2024/01/01 00:00:00] ppocr INFO: dt_boxes num : 2",
            "[2024/01/01 00:00:00] ppocr INFO: ('Hello world', 0.9985)",
            "[2024/01/01 00:00:00] ppocr INFO: (\"Second line\", 0.93)",
            "some noise without a tuple"
        });

        var text = PaddleOcrEngine.ParseRecognizedText(stdout);

        Assert.Contains("Hello world", text);
        Assert.Contains("Second line", text);
        Assert.DoesNotContain("noise", text);
    }

    [Fact]
    public void ParseRecognizedText_EmptyStdout_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PaddleOcrEngine.ParseRecognizedText(""));
    }

    [Fact]
    public void BuildArguments_IncludesImageDir()
    {
        var args = PaddleOcrEngine.BuildArguments("C:/tmp/scan.png");

        Assert.Contains("--image_dir", args);
        Assert.Contains("C:/tmp/scan.png", args);
    }

    [Fact]
    public async Task IsAvailable_FalseWhenPathMissing()
    {
        var engine = new PaddleOcrEngine(new FakeRunner(_ => Ok("")), executablePath: null);
        Assert.False(await engine.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecognizeImage_ParsesText_OnSuccess()
    {
        using var temp = TempDir();
        var exe = Path.Combine(temp, "paddleocr.exe");
        File.WriteAllText(exe, "stub");
        var image = Path.Combine(temp, "scan.png");
        File.WriteAllText(image, "img");

        var engine = new PaddleOcrEngine(
            new FakeRunner(_ => Ok("ppocr INFO: ('Recognised text', 0.99)")),
            exe);

        var result = await engine.RecognizeImageAsync(image, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Recognised text", result.Text);
    }

    [Fact]
    public async Task RecognizeImage_FailsOnNonZeroExit()
    {
        using var temp = TempDir();
        var exe = Path.Combine(temp, "paddleocr.exe");
        File.WriteAllText(exe, "stub");
        var image = Path.Combine(temp, "scan.png");
        File.WriteAllText(image, "img");

        var engine = new PaddleOcrEngine(
            new FakeRunner(_ => new ProcessRunResult(1, false, "", "boom", TimeSpan.Zero)),
            exe);

        var result = await engine.RecognizeImageAsync(image, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("boom", result.ErrorMessage);
    }

    private static ProcessRunResult Ok(string stdout)
        => new(0, false, stdout, "", TimeSpan.Zero);

    private static string TempDirPath() => Path.Combine(
        Path.GetTempPath(), "SnappyDocsConvertPaddleTests", Guid.NewGuid().ToString("N"));

    private static DisposableDir TempDir()
    {
        var path = TempDirPath();
        Directory.CreateDirectory(path);
        return new DisposableDir(path);
    }

    private sealed class DisposableDir : IDisposable
    {
        private readonly string _path;
        public DisposableDir(string path) => _path = path;
        public static implicit operator string(DisposableDir d) => d._path;
        public void Dispose()
        {
            try { if (Directory.Exists(_path)) Directory.Delete(_path, true); } catch { }
        }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly Func<IReadOnlyList<string>, ProcessRunResult> _handler;
        public FakeRunner(Func<IReadOnlyList<string>, ProcessRunResult> handler) => _handler = handler;

        public Task<ProcessRunResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => Task.FromResult(_handler(arguments));
    }
}

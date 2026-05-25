using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Office;
using SnappyDocsConvert.Core.Services.Paths;

namespace SnappyDocsConvert.Tests;

public sealed class OfficeComEngineTests
{
    [Fact]
    public async Task AvailabilityProvider_ReturnsFalse_WhenProgIdResolverReturnsNull()
    {
        var provider = new MicrosoftOfficeAvailabilityProvider(new FakeProgIdResolver(_ => null));

        var availability = await provider.GetOfficeAvailabilityAsync(CancellationToken.None);

        Assert.False(availability.WordAvailable);
        Assert.False(availability.PowerPointAvailable);
        Assert.False(availability.CanConvertWordDocuments);
        Assert.False(availability.CanConvertPowerPointPresentations);
    }

    [Fact]
    public async Task AvailabilityProvider_ReturnsWordAvailable_WhenWordProgIdExists()
    {
        var provider = new MicrosoftOfficeAvailabilityProvider(
            new FakeProgIdResolver(progId => progId == OfficeComProgIds.WordApplication ? typeof(object) : null));

        var availability = await provider.GetOfficeAvailabilityAsync(CancellationToken.None);

        Assert.True(availability.WordAvailable);
        Assert.False(availability.PowerPointAvailable);
        Assert.True(availability.CanConvertWordDocuments);
    }

    [Fact]
    public async Task AvailabilityProvider_ReturnsPowerPointAvailable_WhenPowerPointProgIdExists()
    {
        var provider = new MicrosoftOfficeAvailabilityProvider(
            new FakeProgIdResolver(progId => progId == OfficeComProgIds.PowerPointApplication ? typeof(object) : null));

        var availability = await provider.GetOfficeAvailabilityAsync(CancellationToken.None);

        Assert.False(availability.WordAvailable);
        Assert.True(availability.PowerPointAvailable);
        Assert.True(availability.CanConvertPowerPointPresentations);
    }

    [Fact]
    public async Task ConvertToPdfAsync_MapsDocxToWord()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var outputDirectory = temp.CreateSubdirectory("out");
        var runner = new FakeOfficeComRunner((_, _, output, _, _) =>
        {
            File.WriteAllText(output, "%PDF");
            return Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(1)));
        });
        var engine = CreateEngine(OfficeAvailabilityFor(word: true, powerPoint: false), runner);

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfficeAppKind.Word, runner.LastAppKind);
    }

    [Fact]
    public async Task ConvertToPdfAsync_MapsPptxToPowerPoint()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("slides.pptx", "fake pptx");
        var outputDirectory = temp.CreateSubdirectory("out");
        var runner = new FakeOfficeComRunner((_, _, output, _, _) =>
        {
            File.WriteAllText(output, "%PDF");
            return Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(1)));
        });
        var engine = CreateEngine(OfficeAvailabilityFor(word: false, powerPoint: true), runner);

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfficeAppKind.PowerPoint, runner.LastAppKind);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForUnsupportedExtension()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.odt", "fake odt");
        var engine = CreateEngine(OfficeAvailabilityFor(word: true, powerPoint: true));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.UnsupportedInput, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForMissingInput()
    {
        using var temp = TempDirectory.Create();
        var engine = CreateEngine(OfficeAvailabilityFor(word: true, powerPoint: true));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(Path.Combine(temp.Path, "missing.docx"), temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.MissingInput, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_WhenOfficeMissing()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(OfficeAvailabilityFor(word: false, powerPoint: false));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.EngineUnavailable, result.Status);
        Assert.Contains("Word COM is not available", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPdfAsync_SuccessfulFakeWordExport_ReturnsPdfResult()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.rtf", "{\\rtf1 test}");
        var outputDirectory = temp.CreateSubdirectory("out");
        var expectedPdf = Path.Combine(outputDirectory, "input.pdf");
        var engine = CreateEngine(
            OfficeAvailabilityFor(word: true, powerPoint: false),
            new FakeOfficeComRunner((_, _, output, _, _) =>
            {
                File.WriteAllText(output, "%PDF");
                return Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(1)));
            }));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionEngineKind.MicrosoftOffice, result.EngineKind);
        Assert.Equal(expectedPdf, result.OutputPdfPath);
    }

    [Fact]
    public async Task ConvertToPdfAsync_SuccessfulFakePowerPointExport_ReturnsPdfResult()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("slides.ppt", "fake ppt");
        var outputDirectory = temp.CreateSubdirectory("out");
        var expectedPdf = Path.Combine(outputDirectory, "slides.pdf");
        var engine = CreateEngine(
            OfficeAvailabilityFor(word: false, powerPoint: true),
            new FakeOfficeComRunner((_, _, output, _, _) =>
            {
                File.WriteAllText(output, "%PDF");
                return Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(1)));
            }));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedPdf, result.OutputPdfPath);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForTimeout()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(
            OfficeAvailabilityFor(word: true, powerPoint: false),
            new FakeOfficeComRunner((_, _, _, _, _) =>
                Task.FromResult(OfficeComExportResult.Timeout(TimeSpan.FromSeconds(180)))));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_WhenOutputMissingAfterExport()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(
            OfficeAvailabilityFor(word: true, powerPoint: false),
            new FakeOfficeComRunner((_, _, _, _, _) =>
                Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(1)))));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.OutputMissing, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_SerializesOfficeComConversions()
    {
        using var temp = TempDirectory.Create();
        var input1 = temp.WriteFile("one.docx", "one");
        var input2 = temp.WriteFile("two.docx", "two");
        var output1 = temp.CreateSubdirectory("out1");
        var output2 = temp.CreateSubdirectory("out2");
        var active = 0;
        var maxActive = 0;
        var runner = new FakeOfficeComRunner(async (_, _, output, _, _) =>
        {
            var current = Interlocked.Increment(ref active);
            maxActive = Math.Max(maxActive, current);
            await Task.Delay(80);
            File.WriteAllText(output, "%PDF");
            Interlocked.Decrement(ref active);
            return OfficeComExportResult.Succeeded(TimeSpan.FromMilliseconds(80));
        });
        var engine = CreateEngine(OfficeAvailabilityFor(word: true, powerPoint: false), runner);

        await Task.WhenAll(
            engine.ConvertToPdfAsync(new ConversionRequest(input1, output1), CancellationToken.None),
            engine.ConvertToPdfAsync(new ConversionRequest(input2, output2), CancellationToken.None));

        Assert.Equal(1, maxActive);
    }

    private static OfficeComConversionEngine CreateEngine(
        OfficeAvailability availability,
        FakeOfficeComRunner? runner = null)
        => new(
            new FakeMicrosoftOfficeAvailabilityProvider(availability),
            runner ?? new FakeOfficeComRunner((_, _, _, _, _) =>
                Task.FromResult(OfficeComExportResult.Succeeded(TimeSpan.Zero))),
            new PathService(),
            new OfficeConversionOptions());

    private static OfficeAvailability OfficeAvailabilityFor(bool word, bool powerPoint)
        => new(
            word,
            powerPoint,
            word,
            powerPoint,
            Array.Empty<string>(),
            Array.Empty<EngineSetupRecommendation>(),
            null);

    private sealed class FakeProgIdResolver : IComProgIdResolver
    {
        private readonly Func<string, Type?> _handler;

        public FakeProgIdResolver(Func<string, Type?> handler)
        {
            _handler = handler;
        }

        public Type? GetTypeFromProgId(string progId) => _handler(progId);
    }

    private sealed class FakeMicrosoftOfficeAvailabilityProvider : IMicrosoftOfficeAvailabilityProvider
    {
        private readonly OfficeAvailability _availability;

        public FakeMicrosoftOfficeAvailabilityProvider(OfficeAvailability availability)
        {
            _availability = availability;
        }

        public Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(_availability.WordAvailable || _availability.PowerPointAvailable
                ? EngineAvailability.Available("Microsoft Office COM")
                : EngineAvailability.Unavailable("Office missing."));

        public Task<OfficeAvailability> GetOfficeAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(_availability);
    }

    private sealed class FakeOfficeComRunner : IOfficeComRunner
    {
        private readonly Func<OfficeAppKind, string, string, TimeSpan, CancellationToken, Task<OfficeComExportResult>> _handler;

        public FakeOfficeComRunner(
            Func<OfficeAppKind, string, string, TimeSpan, CancellationToken, Task<OfficeComExportResult>> handler)
        {
            _handler = handler;
        }

        public OfficeAppKind? LastAppKind { get; private set; }

        public Task<OfficeComExportResult> ExportToPdfAsync(
            OfficeAppKind appKind,
            string inputPath,
            string outputPdfPath,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastAppKind = appKind;
            return _handler(appKind, inputPath, outputPdfPath, timeout, cancellationToken);
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
                "SnappyDocsConvertOfficeTests",
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
}

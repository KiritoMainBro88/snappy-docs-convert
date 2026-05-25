using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

public sealed class BatchConversionPipelineTests
{
    [Fact]
    public void OutputPlanner_PlansDocxPdfPath()
    {
        using var temp = TempDirectory.Create();
        var inputPath = Path.Combine(temp.Path, "Report.docx");
        var outputRoot = temp.CreateSubdirectory("out");
        var planner = new BatchOutputPlanner();

        var plan = planner.PlanItem(new BatchConversionItem(inputPath), Options(outputRoot));

        Assert.Equal("Report", plan.SafeBaseName);
        Assert.StartsWith(Path.Combine(outputRoot, "pdf"), plan.PdfOutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".pdf", plan.PdfOutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__", Path.GetFileNameWithoutExtension(plan.PdfOutputPath), StringComparison.Ordinal);
    }

    [Fact]
    public void OutputPlanner_PlansPdfImageFolder()
    {
        using var temp = TempDirectory.Create();
        var inputPath = Path.Combine(temp.Path, "input.pdf");
        var outputRoot = temp.CreateSubdirectory("out");
        var planner = new BatchOutputPlanner();

        var plan = planner.PlanItem(new BatchConversionItem(inputPath), Options(outputRoot));

        Assert.StartsWith(Path.Combine(outputRoot, "images"), plan.ImageOutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input__", plan.ImageOutputDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("slides.pptx", "slide")]
    [InlineData("deck.odp", "slide")]
    [InlineData("doc.docx", "page")]
    [InlineData("input.pdf", "page")]
    public void OutputPlanner_SelectsPagePrefix(string fileName, string expectedPrefix)
    {
        using var temp = TempDirectory.Create();
        var planner = new BatchOutputPlanner();

        var plan = planner.PlanItem(
            new BatchConversionItem(Path.Combine(temp.Path, fileName)),
            Options(temp.CreateSubdirectory("out")));

        Assert.Equal(expectedPrefix, plan.PagePrefix);
    }

    [Fact]
    public void OutputPlanner_DuplicateBaseNamesGetDifferentHash()
    {
        using var temp = TempDirectory.Create();
        var planner = new BatchOutputPlanner();
        var outputRoot = temp.CreateSubdirectory("out");

        var one = planner.PlanItem(new BatchConversionItem(Path.Combine(temp.Path, "a", "same.docx")), Options(outputRoot));
        var two = planner.PlanItem(new BatchConversionItem(Path.Combine(temp.Path, "b", "same.docx")), Options(outputRoot));

        Assert.Equal(one.SafeBaseName, two.SafeBaseName);
        Assert.NotEqual(one.Hash8, two.Hash8);
        Assert.NotEqual(one.PdfOutputPath, two.PdfOutputPath);
    }

    [Fact]
    public void OutputPlanner_SanitizesInvalidPathChars()
    {
        using var temp = TempDirectory.Create();
        var planner = new BatchOutputPlanner();

        var plan = planner.PlanItem(
            new BatchConversionItem(Path.Combine(temp.Path, "bad:name?.docx")),
            Options(temp.CreateSubdirectory("out")));

        Assert.DoesNotContain(":", plan.SafeBaseName);
        Assert.DoesNotContain("?", plan.SafeBaseName);
    }

    [Fact]
    public void OutputPlanner_OutputStaysInsideRoot()
    {
        using var temp = TempDirectory.Create();
        var outputRoot = temp.CreateSubdirectory("out");
        var planner = new BatchOutputPlanner();

        var plan = planner.PlanItem(
            new BatchConversionItem(Path.Combine(temp.Path, "..", "evil.docx")),
            Options(outputRoot));

        Assert.StartsWith(Path.GetFullPath(outputRoot), plan.PdfOutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(outputRoot), plan.ImageOutputDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EngineSelector_PdfNeedsNoEngine()
    {
        var selector = Selector(OfficeAvailability(word: true, powerPoint: true), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync("input.pdf", Options("out"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.EngineKind);
    }

    [Fact]
    public async Task EngineSelector_AutoDocxChoosesOffice_WhenAvailable()
    {
        var selector = Selector(OfficeAvailability(word: true, powerPoint: false), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync("input.docx", Options("out"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionEngineKind.MicrosoftOffice, result.EngineKind);
    }

    [Fact]
    public async Task EngineSelector_AutoDocxFallsBackLibreOffice_WhenOfficeMissing()
    {
        var selector = Selector(OfficeAvailability(word: false, powerPoint: false), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync("input.docx", Options("out"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionEngineKind.LibreOffice, result.EngineKind);
    }

    [Fact]
    public async Task EngineSelector_AutoPptxChoosesPowerPoint_WhenAvailable()
    {
        var selector = Selector(OfficeAvailability(word: false, powerPoint: true), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync("slides.pptx", Options("out"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionEngineKind.MicrosoftOffice, result.EngineKind);
    }

    [Fact]
    public async Task EngineSelector_AutoOdtChoosesLibreOffice()
    {
        var selector = Selector(OfficeAvailability(word: true, powerPoint: true), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync("input.odt", Options("out"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionEngineKind.LibreOffice, result.EngineKind);
    }

    [Fact]
    public async Task EngineSelector_ForcedOfficeMissingFails()
    {
        var selector = Selector(OfficeAvailability(word: false, powerPoint: false), EngineAvailability.Available("soffice"));

        var result = await selector.SelectAsync(
            "input.docx",
            Options("out") with { EnginePreference = BatchConversionEnginePreference.MicrosoftOffice },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Microsoft Office", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EngineSelector_ForcedLibreOfficeMissingFails()
    {
        var selector = Selector(OfficeAvailability(word: true, powerPoint: true), EngineAvailability.Unavailable("missing"));

        var result = await selector.SelectAsync(
            "input.docx",
            Options("out") with { EnginePreference = BatchConversionEnginePreference.LibreOffice },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("LibreOffice", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EngineSelector_NoEngineGivesSetupGuidance()
    {
        var selector = Selector(OfficeAvailability(word: false, powerPoint: false), EngineAvailability.Unavailable("missing"));

        var result = await selector.SelectAsync("input.docx", Options("out"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Recommendations, item => item.Contains("LibreOffice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pipeline_PdfToImages_CallsRendererOnly()
    {
        using var temp = TempDirectory.Create();
        var pdfPath = temp.WriteFile("input.pdf", "%PDF");
        var renderer = new FakePdfRenderer();
        var office = new FakeOfficeEngine();
        var libre = new FakeLibreOfficeEngine();
        var selector = new FakeSelector(_ => throw new InvalidOperationException("selector should not run"));
        var pipeline = Pipeline(selector, libre, office, renderer);

        var result = await pipeline.RunAsync(
            Job(pdfPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, office.CallCount + libre.CallCount);
        Assert.Single(renderer.Requests);
        Assert.Null(Assert.Single(result.Items).OutputPdfPath);
    }

    [Fact]
    public async Task Pipeline_PdfToPdfAndImages_CopiesPdfAndRenders()
    {
        using var temp = TempDirectory.Create();
        var pdfPath = temp.WriteFile("input.pdf", "%PDF");
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(pdfPath, temp.CreateSubdirectory("out"), BatchConversionTarget.PdfAndImages),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.Status == BatchConversionItemStatus.Succeeded, item.ErrorMessage);
        Assert.NotNull(item.OutputPdfPath);
        Assert.True(File.Exists(item.OutputPdfPath));
        Assert.Equal(item.OutputPdfPath, renderer.Requests.Single().InputPdfPath);
    }

    [Fact]
    public async Task Pipeline_DocxToPdf_CallsSelectedDocumentEngine()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = new FakeOfficeEngine();
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(
            new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            office: office,
            renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Success, item.ErrorMessage);
        Assert.Equal(1, office.CallCount);
        Assert.Empty(renderer.Requests);
        Assert.NotNull(item.OutputPdfPath);
        Assert.True(File.Exists(item.OutputPdfPath));
    }

    [Fact]
    public async Task Pipeline_DocxToImages_ConvertsTempPdfThenDeletesIt()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(
            new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            office: new FakeOfficeEngine(),
            renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Success, item.ErrorMessage);
        Assert.Null(item.OutputPdfPath);
        Assert.False(File.Exists(renderer.Requests.Single().InputPdfPath));
    }

    [Fact]
    public async Task Pipeline_DocxToPdfAndImages_RendersFromFinalPdf()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(
            new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            office: new FakeOfficeEngine(),
            renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.PdfAndImages),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Success, item.ErrorMessage);
        Assert.Equal(item.OutputPdfPath, renderer.Requests.Single().InputPdfPath);
    }

    [Fact]
    public async Task Pipeline_PptxImages_UseSlidePrefix()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("slides.pptx", "deck");
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(
            new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            office: new FakeOfficeEngine(),
            renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            CancellationToken.None);

        Assert.True(result.Success, Assert.Single(result.Items).ErrorMessage);
        Assert.Equal("slide", renderer.Requests.Single().Options.PagePrefix);
    }

    [Fact]
    public async Task Pipeline_FailedItemDoesNotStopNextItem()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WriteFile("bad.docx", "bad");
        var second = temp.WriteFile("good.docx", "good");
        var selector = new FakeSelector(path => path.Contains("bad", StringComparison.OrdinalIgnoreCase)
            ? BatchEngineSelectionResult.Failed("no engine")
            : BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice));
        var pipeline = Pipeline(selector, office: new FakeOfficeEngine());

        var result = await pipeline.RunAsync(
            Job(new[] { first, second }, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.SucceededCount);
    }

    [Fact]
    public async Task Pipeline_MissingFileResultFailed()
    {
        using var temp = TempDirectory.Create();
        var pipeline = Pipeline();

        var result = await pipeline.RunAsync(
            Job(Path.Combine(temp.Path, "missing.pdf"), temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Failed, item.Status);
        Assert.Contains("does not exist", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pipeline_NoEngineResultFailedWithSetupGuidance()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var pipeline = Pipeline(new FakeSelector(_ => BatchEngineSelectionResult.Failed(
            "no engine",
            new[] { "Install LibreOffice" })));

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Failed, item.Status);
        Assert.Contains(item.Warnings, warning => warning.Contains("LibreOffice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pipeline_CancellationBeforeStartCancelsAll()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WriteFile("one.pdf", "%PDF");
        var second = temp.WriteFile("two.pdf", "%PDF");
        var pipeline = Pipeline();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await pipeline.RunAsync(
            Job(new[] { first, second }, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            cts.Token);

        Assert.Equal(2, result.CancelledCount);
        Assert.All(result.Items, item => Assert.Equal(BatchConversionItemStatus.Cancelled, item.Status));
    }

    [Fact]
    public async Task Pipeline_CancellationAfterFirstItemKeepsFirstSuccessAndCancelsRest()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WriteFile("one.pdf", "%PDF");
        var second = temp.WriteFile("two.pdf", "%PDF");
        using var cts = new CancellationTokenSource();
        var renderer = new FakePdfRenderer
        {
            AfterRender = () => cts.Cancel()
        };
        var pipeline = Pipeline(renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(new[] { first, second }, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            cts.Token);

        Assert.Equal(BatchConversionItemStatus.Succeeded, result.Items[0].Status);
        Assert.Equal(BatchConversionItemStatus.Cancelled, result.Items[1].Status);
    }

    [Fact]
    public async Task Pipeline_TempPdfKeptWhenOptionTrue()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var renderer = new FakePdfRenderer();
        var pipeline = Pipeline(
            new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            office: new FakeOfficeEngine(),
            renderer: renderer);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Images, keepIntermediatePdf: true),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Success, item.ErrorMessage);
        Assert.NotNull(item.OutputPdfPath);
        Assert.True(File.Exists(item.OutputPdfPath));
        Assert.Equal(item.OutputPdfPath, renderer.Requests.Single().InputPdfPath);
    }

    [Fact]
    public async Task Pipeline_SummaryCountsCorrect()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WriteFile("one.pdf", "%PDF");
        var second = Path.Combine(temp.Path, "missing.pdf");
        var pipeline = Pipeline();

        var result = await pipeline.RunAsync(
            Job(new[] { first, second }, temp.CreateSubdirectory("out"), BatchConversionTarget.Images),
            null,
            CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.Success);
    }

    private static BatchConversionPipeline Pipeline(
        FakeSelector? selector = null,
        FakeLibreOfficeEngine? libre = null,
        FakeOfficeEngine? office = null,
        FakePdfRenderer? renderer = null)
        => new(
            new BatchOutputPlanner(),
            selector ?? new FakeSelector(_ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice)),
            libre ?? new FakeLibreOfficeEngine(),
            office ?? new FakeOfficeEngine(),
            renderer ?? new FakePdfRenderer());

    private static ConversionEngineSelector Selector(
        OfficeAvailability officeAvailability,
        EngineAvailability libreOfficeAvailability)
        => new(
            new FakeMicrosoftOfficeAvailabilityProvider(officeAvailability),
            new FakeLibreOfficeLocator(libreOfficeAvailability));

    private static BatchConversionJob Job(
        string inputPath,
        string outputRoot,
        BatchConversionTarget target,
        bool keepIntermediatePdf = false)
        => Job(new[] { inputPath }, outputRoot, target, keepIntermediatePdf);

    private static BatchConversionJob Job(
        IReadOnlyList<string> inputPaths,
        string outputRoot,
        BatchConversionTarget target,
        bool keepIntermediatePdf = false)
        => new(
            inputPaths.Select(path => new BatchConversionItem(path)).ToArray(),
            Options(outputRoot) with
            {
                Target = target,
                KeepIntermediatePdf = keepIntermediatePdf
            });

    private static BatchConversionOptions Options(string outputRoot)
        => new() { OutputRoot = outputRoot };

    private static OfficeAvailability OfficeAvailability(bool word, bool powerPoint)
        => new(
            word,
            powerPoint,
            word,
            powerPoint,
            Array.Empty<string>(),
            Array.Empty<EngineSetupRecommendation>(),
            null);

    private sealed class FakeSelector : IConversionEngineSelector
    {
        private readonly Func<string, BatchEngineSelectionResult> _select;

        public FakeSelector(Func<string, BatchEngineSelectionResult> select)
        {
            _select = select;
        }

        public Task<BatchEngineSelectionResult> SelectAsync(
            string inputPath,
            BatchConversionOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_select(inputPath));
    }

    private sealed class FakeLibreOfficeEngine : ILibreOfficeConversionEngine
    {
        public int CallCount { get; private set; }

        public Task<ConversionResult> ConvertToPdfAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(FakeConversion(request, ConversionEngineKind.LibreOffice));
        }
    }

    private sealed class FakeOfficeEngine : IOfficeComConversionEngine
    {
        public int CallCount { get; private set; }

        public Task<ConversionResult> ConvertToPdfAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(FakeConversion(request, ConversionEngineKind.MicrosoftOffice));
        }
    }

    private sealed class FakePdfRenderer : IPdfImageRenderer
    {
        public List<PdfRenderRequest> Requests { get; } = new();

        public Action? AfterRender { get; init; }

        public Task<PdfRenderResult> RenderAsync(
            PdfRenderRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Directory.CreateDirectory(request.OutputDirectory);
            var extension = request.Options.Format switch
            {
                ImageOutputFormat.Jpeg => ".jpg",
                ImageOutputFormat.Webp => ".webp",
                _ => ".png"
            };
            var outputPath = Path.Combine(request.OutputDirectory, $"{request.Options.PagePrefix}-001{extension}");
            File.WriteAllText(outputPath, "image");
            AfterRender?.Invoke();

            return Task.FromResult(new PdfRenderResult
            {
                Success = true,
                InputPdfPath = request.InputPdfPath,
                OutputDirectory = request.OutputDirectory,
                PagesRendered = 1,
                OutputFiles = new[] { outputPath }
            });
        }
    }

    private sealed class FakeMicrosoftOfficeAvailabilityProvider : IMicrosoftOfficeAvailabilityProvider
    {
        private readonly OfficeAvailability _availability;

        public FakeMicrosoftOfficeAvailabilityProvider(OfficeAvailability availability)
        {
            _availability = availability;
        }

        public Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(_availability.CanConvertWordDocuments || _availability.CanConvertPowerPointPresentations
                ? EngineAvailability.Available("Microsoft Office COM")
                : EngineAvailability.Unavailable("Office unavailable"));

        public Task<OfficeAvailability> GetOfficeAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(_availability);
    }

    private sealed class FakeLibreOfficeLocator : ILibreOfficeLocator
    {
        private readonly EngineAvailability _availability;

        public FakeLibreOfficeLocator(EngineAvailability availability)
        {
            _availability = availability;
        }

        public Task<EngineAvailability> LocateAsync(
            LibreOfficeOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_availability);
    }

    private static ConversionResult FakeConversion(
        ConversionRequest request,
        ConversionEngineKind engineKind)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var outputPath = Path.Combine(
            request.OutputDirectory,
            $"{Path.GetFileNameWithoutExtension(request.InputPath)}.pdf");
        File.WriteAllText(outputPath, "%PDF");

        return new ConversionResult
        {
            Success = true,
            Status = ConversionStatus.Succeeded,
            InputPath = request.InputPath,
            OutputPdfPath = outputPath,
            EngineKind = engineKind
        };
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
                "SnappyDocsConvertBatchTests",
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
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
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

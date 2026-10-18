using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

public sealed class BatchConversionPipelineTests
{
    [Fact]
    public void InputScanner_AcceptsSupportedFilesAndRejectsUnsupported()
    {
        using var temp = TempDirectory.Create();
        var pdf = temp.WriteFile("input.pdf", "%PDF");
        var ignored = temp.WriteFile("ignored.txt", "no");
        var scanner = new BatchInputScanner();

        var result = scanner.Scan(new[] { pdf, ignored });

        Assert.Contains(pdf, result.AcceptedFiles);
        Assert.Contains(ignored, result.RejectedPaths);
    }

    [Fact]
    public void InputScanner_RecursesFoldersAndDeduplicates()
    {
        using var temp = TempDirectory.Create();
        var folder = temp.CreateSubdirectory("folder");
        var docx = Path.Combine(folder, "input.docx");
        File.WriteAllText(docx, "doc");
        var scanner = new BatchInputScanner();

        var result = scanner.Scan(new[] { folder, docx });

        Assert.Single(result.AcceptedFiles);
        Assert.Equal(docx, Assert.Single(result.AcceptedFiles));
    }

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
        var selector = new FakeSelector(path => Path.GetFileName(path).Contains("bad", StringComparison.OrdinalIgnoreCase)
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

    // ---------------------------------------------------------------------------------------------
    // Property 1: Fallback fires exactly on a recoverable failure with an eligible alternate when
    //             enabled.
    // Validates: Requirements 1.1, 1.2, 2.2, 2.3, 3.3, 4.1, 4.2, 4.3, 5.3
    //
    // Drives the REAL BatchConversionPipeline over the random matrix of
    // (primaryStatus, officeAvail, libreAvail, preference, extension) with EnableEngineFallback = true.
    // The FakeSelector returns the primary engine (per preference/availability) from SelectAsync and
    // the derived ordered eligible set from GetEligibleEnginesAsync; the primary engine fake returns
    // primaryStatus while the alternate engine fake returns success, so we can observe whether the
    // fallback engine is invoked and whether DocumentEngine flips to the alternate.
    //
    // Implemented as a deterministic, seeded xUnit loop (>= 100 iterations, fixed seed recorded in
    // assertion messages); hand-written fakes, no new PBT dependency, CPU-only, no real
    // Office/LibreOffice (per AGENTS.md).
    // ---------------------------------------------------------------------------------------------
    private const int Property1Iterations = 200;
    private const int Property1Seed = 20240315;

    private static readonly ConversionStatus[] RecoverableFailureStatuses =
    {
        ConversionStatus.Failed,
        ConversionStatus.ProcessFailed,
        ConversionStatus.TimedOut,
        ConversionStatus.OutputMissing,
        ConversionStatus.EngineUnavailable
    };

    // Primary statuses exercised by Property 1: success, every recoverable failure, and the
    // non-recoverable, non-cancellation failures. Cancellation is owned by Property 3 (task 3.4).
    private static readonly ConversionStatus[] Property1PrimaryStatuses =
    {
        ConversionStatus.Succeeded,
        ConversionStatus.Failed,
        ConversionStatus.ProcessFailed,
        ConversionStatus.TimedOut,
        ConversionStatus.OutputMissing,
        ConversionStatus.EngineUnavailable,
        ConversionStatus.MissingInput,
        ConversionStatus.UnsupportedInput,
        ConversionStatus.OutputCollision
    };

    // Document inputs only: two-engine formats plus single-engine LibreOffice-only formats
    // (.odt/.odp) that can never have an Office alternate.
    private static readonly string[] Property1Extensions =
        { ".doc", ".docx", ".rtf", ".ppt", ".pptx", ".odt", ".odp" };

    private static readonly string[] Property1OfficeWordExtensions = { ".doc", ".docx", ".rtf" };
    private static readonly string[] Property1OfficePowerPointExtensions = { ".ppt", ".pptx" };

    private static readonly BatchConversionEnginePreference[] Property1Preferences =
    {
        BatchConversionEnginePreference.Auto,
        BatchConversionEnginePreference.MicrosoftOffice,
        BatchConversionEnginePreference.LibreOffice
    };

    [Fact]
    public async Task Property1_FallbackFiresIffRecoverableFailureWithEligibleAlternate()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property1Seed);

        for (var i = 0; i < Property1Iterations; i++)
        {
            var extension = Property1Extensions[random.Next(Property1Extensions.Length)];
            var officeAvail = random.Next(2) == 1;
            var libreAvail = random.Next(2) == 1;
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];
            var primaryStatus = Property1PrimaryStatuses[random.Next(Property1PrimaryStatuses.Length)];

            // Reference eligibility predicate (independent of the production selector wiring).
            var officeSupported = Property1OfficeWordExtensions.Contains(extension)
                || Property1OfficePowerPointExtensions.Contains(extension);
            var officeEligible = officeSupported && officeAvail;
            var libreEligible = libreAvail; // every Property1 extension is LibreOffice-supported.

            // Ordered eligible set: Office first unless preference forces LibreOffice (mirrors the
            // real ConversionEngineSelector.GetEligibleEnginesAsync ordering).
            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = new List<ConversionEngineKind>(2);
            if (officeFirst)
            {
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
            }
            else
            {
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
            }

            // Primary engine that SelectAsync would choose (null => selection fails, no engine runs).
            ConversionEngineKind? primary = preference switch
            {
                BatchConversionEnginePreference.MicrosoftOffice =>
                    officeEligible ? ConversionEngineKind.MicrosoftOffice : (ConversionEngineKind?)null,
                BatchConversionEnginePreference.LibreOffice =>
                    libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null,
                _ => officeEligible
                    ? ConversionEngineKind.MicrosoftOffice
                    : (libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null)
            };

            var primaryError = $"primary {primaryStatus} failure #{i}";

            // Configure each engine fake by its role: the primary returns primaryStatus; the other
            // (the potential alternate) always succeeds so we can observe a fallback flip.
            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(ConversionEngineKind kind)
                => (request, _) =>
                    primary is not null && kind == primary.Value
                        ? (primaryStatus == ConversionStatus.Succeeded
                            ? FakeConversion(request, kind)
                            : FakeFailure(request, kind, primaryStatus, primaryError))
                        : FakeConversion(request, kind);

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedPrimary = primary;
            var capturedEligible = eligible.ToArray();
            var selector = new FakeSelector(
                _ => capturedPrimary is null
                    ? BatchEngineSelectionResult.Failed("No engine available.")
                    : BatchEngineSelectionResult.Selected(capturedPrimary.Value),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var inputPath = temp.WriteFile($"input_{i}{extension}", "doc");
            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = true
            };
            var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

            var context =
                $"seed={Property1Seed}, iteration={i}, ext={extension}, officeAvail={officeAvail}, " +
                $"libreAvail={libreAvail}, pref={preference}, primaryStatus={primaryStatus}, " +
                $"primary={(primary is null ? "none" : primary.Value.ToString())}, " +
                $"eligible=[{string.Join(",", eligible)}]";

            var result = await pipeline.RunAsync(job, null, CancellationToken.None);
            var item = Assert.Single(result.Items);

            // Selection failed: no engine ran and the item failed without any fallback (Req 2.3).
            if (primary is null)
            {
                Assert.True(
                    office.CallCount == 0 && libre.CallCount == 0,
                    $"Expected no engine invocation when selection fails. " +
                    $"office={office.CallCount}, libre={libre.CallCount}. {context}");
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed when selection fails, got {item.Status}. {context}");
                continue;
            }

            var primaryFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            // The primary engine always runs exactly once.
            Assert.True(
                primaryFake.CallCount == 1,
                $"Expected primary engine to run exactly once, got {primaryFake.CallCount}. {context}");

            var primaryRecoverable = primaryStatus != ConversionStatus.Succeeded
                && RecoverableFailureStatuses.Contains(primaryStatus);
            var alternate = eligible.FirstOrDefault(e => e != primary.Value, primary.Value);
            var alternateExists = eligible.Any(e => e != primary.Value);
            var expectFallback = primaryRecoverable && alternateExists;

            // Core IFF: the fallback engine is invoked exactly when the primary result is a
            // Recoverable_Failure AND a distinct eligible alternate exists (Req 1.1, 2.2, 3.3, 4.1-4.3).
            Assert.True(
                (fallbackFake.CallCount > 0) == expectFallback,
                $"Fallback invocation mismatch. invoked={fallbackFake.CallCount > 0}, " +
                $"expected={expectFallback} (recoverable={primaryRecoverable}, " +
                $"alternateExists={alternateExists}). {context}");

            if (primaryStatus == ConversionStatus.Succeeded)
            {
                // Primary success short-circuits before any fallback (Req 4.x success path).
                Assert.True(
                    item.Status == BatchConversionItemStatus.Succeeded,
                    $"Expected Succeeded on primary success, got {item.Status} ({item.ErrorMessage}). {context}");
                Assert.True(
                    item.DocumentEngine == primary.Value,
                    $"Expected DocumentEngine={primary.Value} on primary success, got {item.DocumentEngine}. {context}");
                Assert.True(
                    fallbackFake.CallCount == 0,
                    $"Expected no fallback on primary success, got {fallbackFake.CallCount}. {context}");
            }
            else if (expectFallback)
            {
                // Fallback succeeds: item Succeeded with DocumentEngine flipped to the alternate
                // (Req 1.1, 1.2, 4.1, 4.2, 4.3).
                Assert.True(
                    fallbackFake.CallCount == 1,
                    $"Expected fallback engine to run exactly once, got {fallbackFake.CallCount}. {context}");
                Assert.True(
                    item.Status == BatchConversionItemStatus.Succeeded,
                    $"Expected Succeeded after fallback success, got {item.Status} ({item.ErrorMessage}). {context}");
                Assert.True(
                    item.DocumentEngine == alternate,
                    $"Expected DocumentEngine={alternate} after fallback, got {item.DocumentEngine}. {context}");
            }
            else
            {
                // Either non-recoverable, or recoverable with no eligible alternate: no fallback, and
                // the item fails with the PRIMARY engine's failure message unchanged (Req 2.2, 3.3, 5.3).
                Assert.True(
                    fallbackFake.CallCount == 0,
                    $"Expected no fallback when no eligible alternate / non-recoverable, " +
                    $"got {fallbackFake.CallCount}. {context}");
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed, got {item.Status}. {context}");
                Assert.True(
                    item.ErrorMessage == primaryError,
                    $"Expected ErrorMessage to equal the primary reason '{primaryError}', " +
                    $"got '{item.ErrorMessage}'. {context}");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Property 2: At most one fallback attempt per file.
    // Validates: Requirements 1.3, 1.4
    //
    // Drives the REAL BatchConversionPipeline over the same random matrix as Property 1
    // (primaryStatus, officeAvail, libreAvail, preference, extension) and additionally varies
    // EnableEngineFallback on/off. Across every generated scenario - primary success, recoverable
    // failure, non-recoverable failure, cancellation, with or without an eligible alternate, and with
    // fallback enabled or disabled - the pipeline must invoke the primary engine at most once and the
    // fallback (alternate) engine at most once for a single file (no third engine attempt).
    //
    // Uses its own fixed seed (distinct from Property 1) and >= 100 deterministic iterations, with the
    // seed + iteration recorded in every assertion message; hand-written fakes, no new PBT dependency,
    // CPU-only, no real Office/LibreOffice (per AGENTS.md). Reuses the task 3.2 fakes verbatim.
    // ---------------------------------------------------------------------------------------------
    private const int Property2Iterations = 200;
    private const int Property2Seed = 20240613;

    // Property 2 exercises the full outcome space, including cancellation, so the single-fallback
    // bound is asserted across every classification (success / recoverable / non-recoverable /
    // cancelled). The alternate engine always succeeds, so a fired fallback runs exactly once.
    private static readonly ConversionStatus[] Property2PrimaryStatuses =
    {
        ConversionStatus.Succeeded,
        ConversionStatus.Failed,
        ConversionStatus.ProcessFailed,
        ConversionStatus.TimedOut,
        ConversionStatus.OutputMissing,
        ConversionStatus.EngineUnavailable,
        ConversionStatus.MissingInput,
        ConversionStatus.UnsupportedInput,
        ConversionStatus.OutputCollision,
        ConversionStatus.Cancelled
    };

    [Fact]
    public async Task Property2_AtMostOneFallbackAttemptPerFile()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property2Seed);

        for (var i = 0; i < Property2Iterations; i++)
        {
            var extension = Property1Extensions[random.Next(Property1Extensions.Length)];
            var officeAvail = random.Next(2) == 1;
            var libreAvail = random.Next(2) == 1;
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];
            var primaryStatus = Property2PrimaryStatuses[random.Next(Property2PrimaryStatuses.Length)];
            var enableFallback = random.Next(2) == 1;

            // Reference eligibility predicate (independent of the production selector wiring); mirrors
            // Property 1 so the FakeSelector hands the pipeline a realistic ordered eligible set.
            var officeSupported = Property1OfficeWordExtensions.Contains(extension)
                || Property1OfficePowerPointExtensions.Contains(extension);
            var officeEligible = officeSupported && officeAvail;
            var libreEligible = libreAvail; // every Property2 extension is LibreOffice-supported.

            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = new List<ConversionEngineKind>(2);
            if (officeFirst)
            {
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
            }
            else
            {
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
            }

            // Primary engine that SelectAsync would choose (null => selection fails, no engine runs).
            ConversionEngineKind? primary = preference switch
            {
                BatchConversionEnginePreference.MicrosoftOffice =>
                    officeEligible ? ConversionEngineKind.MicrosoftOffice : (ConversionEngineKind?)null,
                BatchConversionEnginePreference.LibreOffice =>
                    libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null,
                _ => officeEligible
                    ? ConversionEngineKind.MicrosoftOffice
                    : (libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null)
            };

            var primaryError = $"primary {primaryStatus} failure #{i}";

            // Primary returns primaryStatus; the alternate always succeeds so a fired fallback runs
            // (and runs exactly once). A Cancelled primary returns a Cancelled result (no throw).
            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(ConversionEngineKind kind)
                => (request, _) =>
                    primary is not null && kind == primary.Value
                        ? (primaryStatus == ConversionStatus.Succeeded
                            ? FakeConversion(request, kind)
                            : FakeFailure(request, kind, primaryStatus, primaryError))
                        : FakeConversion(request, kind);

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedPrimary = primary;
            var capturedEligible = eligible.ToArray();
            var selector = new FakeSelector(
                _ => capturedPrimary is null
                    ? BatchEngineSelectionResult.Failed("No engine available.")
                    : BatchEngineSelectionResult.Selected(capturedPrimary.Value),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var inputPath = temp.WriteFile($"input_{i}{extension}", "doc");
            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = enableFallback
            };
            var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

            var context =
                $"seed={Property2Seed}, iteration={i}, ext={extension}, officeAvail={officeAvail}, " +
                $"libreAvail={libreAvail}, pref={preference}, primaryStatus={primaryStatus}, " +
                $"enableFallback={enableFallback}, " +
                $"primary={(primary is null ? "none" : primary.Value.ToString())}, " +
                $"eligible=[{string.Join(",", eligible)}]";

            await pipeline.RunAsync(job, null, CancellationToken.None);

            if (primary is null)
            {
                // Selection failed: neither engine is invoked, so the single-fallback bound trivially
                // holds (no primary attempt, no fallback attempt).
                Assert.True(
                    office.CallCount <= 1,
                    $"Office engine invoked more than once when selection failed: {office.CallCount}. {context}");
                Assert.True(
                    libre.CallCount <= 1,
                    $"LibreOffice engine invoked more than once when selection failed: {libre.CallCount}. {context}");
                continue;
            }

            var primaryFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            // Core single-fallback bound (Req 1.3, 1.4): the primary engine runs at most once and the
            // fallback (alternate) engine runs at most once - never a third engine attempt for a file.
            Assert.True(
                primaryFake.CallCount <= 1,
                $"Primary engine invoked more than once: {primaryFake.CallCount}. {context}");
            Assert.True(
                fallbackFake.CallCount <= 1,
                $"Fallback engine invoked more than once: {fallbackFake.CallCount}. {context}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Property 3: Cancellation never triggers or continues fallback and yields Cancelled.
    // Validates: Requirements 2.1, 6.1, 6.3
    //
    // Drives the REAL BatchConversionPipeline over a random matrix of (cancellationForm, extension,
    // preference) with EnableEngineFallback = true and BOTH engines eligible, so that - absent
    // cancellation - a fallback would otherwise fire. The three cancellation forms exercised are:
    //
    //   1. PrimaryCancelled       - the primary engine returns a ConversionResult with
    //                               Status == Cancelled (Req 2.1). The pipeline short-circuits before
    //                               any fallback, so the alternate engine is never invoked.
    //   2. TokenPreCancelled      - the primary engine returns a recoverable failure but cancels the
    //                               shared CancellationTokenSource inside its behavior; by the time the
    //                               pipeline re-checks the token before the fallback attempt it is
    //                               signalled, so the fallback engine is NOT started (Req 6.1).
    //   3a. FallbackCancelledResult - the fallback engine returns a ConversionResult with
    //                                 Status == Cancelled (Req 6.3).
    //   3b. FallbackThrowsOce       - the fallback engine cancels the shared token then throws
    //                                 OperationCanceledException; the pipeline's outer catch maps it to
    //                                 Cancelled because the RunAsync token is signalled (Req 6.3).
    //
    // For every form the item Status MUST be Cancelled and MUST NOT be Failed (cancellation is never
    // reclassified as an engine failure). For forms 1 and 2 the fallback engine MUST NOT be invoked
    // (CallCount == 0); for form 2 this is the explicit Req 6.1 guarantee.
    //
    // Uses its own fixed seed (distinct from Properties 1 and 2) and >= 100 deterministic iterations,
    // with the seed + iteration recorded in every assertion message; hand-written fakes, no new PBT
    // dependency, CPU-only, no real Office/LibreOffice (per AGENTS.md). Reuses the task 3.2 fakes.
    // ---------------------------------------------------------------------------------------------
    private const int Property3Iterations = 200;
    private const int Property3Seed = 20240920;

    private enum Property3CancellationForm
    {
        PrimaryCancelled,
        TokenPreCancelled,
        FallbackCancelledResult,
        FallbackThrowsOce
    }

    private static readonly Property3CancellationForm[] Property3Forms =
    {
        Property3CancellationForm.PrimaryCancelled,
        Property3CancellationForm.TokenPreCancelled,
        Property3CancellationForm.FallbackCancelledResult,
        Property3CancellationForm.FallbackThrowsOce
    };

    // Two-engine formats only: with both engines available a distinct eligible alternate always
    // exists, so - absent cancellation - a fallback would fire and we can prove cancellation suppresses
    // it. Single-engine (.odt/.odp) formats are owned by other properties.
    private static readonly string[] Property3TwoEngineExtensions =
        { ".doc", ".docx", ".rtf", ".ppt", ".pptx" };

    [Fact]
    public async Task Property3_CancellationYieldsCancelledAndNoFallbackFailure()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property3Seed);

        for (var i = 0; i < Property3Iterations; i++)
        {
            var form = Property3Forms[random.Next(Property3Forms.Length)];
            var extension = Property3TwoEngineExtensions[random.Next(Property3TwoEngineExtensions.Length)];
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];

            // Both engines available => both eligible regardless of preference, guaranteeing a distinct
            // eligible alternate so the fallback path is reachable in the absence of cancellation.
            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = officeFirst
                ? new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice }
                : new[] { ConversionEngineKind.LibreOffice, ConversionEngineKind.MicrosoftOffice };

            var primary = officeFirst
                ? ConversionEngineKind.MicrosoftOffice
                : ConversionEngineKind.LibreOffice;
            var alternate = primary == ConversionEngineKind.MicrosoftOffice
                ? ConversionEngineKind.LibreOffice
                : ConversionEngineKind.MicrosoftOffice;

            // A recoverable failure status used by forms 2/3 so the pipeline would otherwise attempt
            // fallback; form 1 overrides the primary with Cancelled directly.
            var recoverableStatus =
                RecoverableFailureStatuses[random.Next(RecoverableFailureStatuses.Length)];
            var primaryError = $"primary {recoverableStatus} failure #{i}";

            // Shared CTS whose token is passed to RunAsync. Forms 2 and 3b cancel it from inside an
            // engine behavior; forms 1 and 3a leave it un-signalled (their cancellation is carried by a
            // Cancelled ConversionResult, which never flows through the OCE catch).
            using var cts = new CancellationTokenSource();

            Func<ConversionRequest, CancellationToken, ConversionResult> primaryBehavior =
                (request, _) =>
                {
                    switch (form)
                    {
                        case Property3CancellationForm.PrimaryCancelled:
                            // Primary itself reports cancellation (Req 2.1).
                            return FakeFailure(
                                request, primary, ConversionStatus.Cancelled, primaryError);
                        case Property3CancellationForm.TokenPreCancelled:
                            // Signal the token after the primary runs so the pipeline's pre-fallback
                            // token check (step 7) sees it cancelled and never starts the fallback.
                            cts.Cancel();
                            return FakeFailure(request, primary, recoverableStatus, primaryError);
                        default:
                            // Forms 3a/3b: a recoverable failure so the fallback attempt begins.
                            return FakeFailure(request, primary, recoverableStatus, primaryError);
                    }
                };

            Func<ConversionRequest, CancellationToken, ConversionResult> fallbackBehavior =
                (request, _) =>
                {
                    switch (form)
                    {
                        case Property3CancellationForm.FallbackCancelledResult:
                            return FakeFailure(
                                request, alternate, ConversionStatus.Cancelled, "fallback cancelled");
                        case Property3CancellationForm.FallbackThrowsOce:
                            // Cancel the RunAsync token first so the pipeline's outer
                            // `catch (OperationCanceledException) when (token.IsCancellationRequested)`
                            // maps the throw to a Cancelled item result (Req 6.3).
                            cts.Cancel();
                            throw new OperationCanceledException(cts.Token);
                        default:
                            // Forms 1/2: the alternate must never be invoked; succeed defensively so a
                            // mistaken invocation would surface as Succeeded (failing the Cancelled
                            // assertion) rather than masking the bug.
                            return FakeConversion(request, alternate);
                    }
                };

            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(
                ConversionEngineKind kind)
                => kind == primary ? primaryBehavior : fallbackBehavior;

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedEligible = eligible;
            var selector = new FakeSelector(
                _ => BatchEngineSelectionResult.Selected(primary),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var inputPath = temp.WriteFile($"input_{i}{extension}", "doc");
            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = true
            };
            var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

            var context =
                $"seed={Property3Seed}, iteration={i}, form={form}, ext={extension}, pref={preference}, " +
                $"recoverableStatus={recoverableStatus}, primary={primary}, alternate={alternate}, " +
                $"eligible=[{string.Join(",", eligible)}]";

            var result = await pipeline.RunAsync(job, null, cts.Token);
            var item = Assert.Single(result.Items);

            var primaryFake = primary == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            // The primary engine always runs exactly once across every cancellation form.
            Assert.True(
                primaryFake.CallCount == 1,
                $"Expected primary engine to run exactly once, got {primaryFake.CallCount}. {context}");

            // Core guarantee: cancellation always yields a Cancelled item result (Req 2.1, 6.1, 6.3).
            Assert.True(
                item.Status == BatchConversionItemStatus.Cancelled,
                $"Expected Status=Cancelled, got {item.Status} ({item.ErrorMessage}). {context}");

            // Cancellation is never reclassified as an engine failure.
            Assert.True(
                item.Status != BatchConversionItemStatus.Failed,
                $"Cancellation must never surface as Failed. {context}");

            if (form is Property3CancellationForm.PrimaryCancelled
                or Property3CancellationForm.TokenPreCancelled)
            {
                // The fallback engine must never be started when the primary is cancelled or the token
                // is already signalled before the fallback attempt begins (Req 2.1, 6.1).
                Assert.True(
                    fallbackFake.CallCount == 0,
                    $"Fallback engine must not be invoked, got {fallbackFake.CallCount}. {context}");
            }
            else
            {
                // Forms 3a/3b begin the fallback attempt exactly once; the cancellation surfaces during
                // (not after) that single attempt (Req 6.3).
                Assert.True(
                    fallbackFake.CallCount == 1,
                    $"Expected fallback engine to run exactly once, got {fallbackFake.CallCount}. {context}");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Property 4: DocumentEngine reflects the producing (or last-attempted) engine.
    // Validates: Requirements 1.2, 5.1, 5.2, 5.4, 5.5
    //
    // Drives the REAL BatchConversionPipeline over the random matrix of
    // (primaryStatus, officeAvail, libreAvail, preference, extension) with EnableEngineFallback = true,
    // and additionally varies whether the ALTERNATE engine also fails so the both-engines-failed path
    // is exercised. For every completed item this asserts:
    //
    //   * On a successful item, DocumentEngine equals the engine that produced the PDF - the primary
    //     when the primary succeeded, the alternate when the fallback produced it (Req 1.2, 5.2).
    //   * On a failed item where a fallback was attempted (both engines failed), DocumentEngine equals
    //     the last engine attempted (the alternate), per Req 5.5, while ErrorMessage retains the PRIMARY
    //     failure reason (matching the task 3.1 implementation detail).
    //   * Whenever a fallback was attempted, result.Fallback is populated (PrimaryEngine == primary,
    //     FallbackEngine == alternate, PrimaryFailureReason retained, correct FallbackSucceeded flag)
    //     and item Warnings contain at least one "Fallback:" detail entry (Req 5.1, 5.4).
    //   * When no fallback was attempted (primary success, non-recoverable failure, or no eligible
    //     alternate), result.Fallback is null and DocumentEngine equals the only engine attempted.
    //
    // Uses its own fixed seed (distinct from Properties 1-3) and >= 100 deterministic iterations, with
    // the seed + iteration recorded in every assertion message; hand-written fakes, no new PBT
    // dependency, CPU-only, no real Office/LibreOffice (per AGENTS.md). Reuses the task 3.2 fakes and
    // the shared Property1 matrix arrays verbatim. Cancellation is owned by Property 3, so the primary
    // status set excludes Cancelled.
    // ---------------------------------------------------------------------------------------------
    private const int Property4Iterations = 200;
    private const int Property4Seed = 20241111;

    [Fact]
    public async Task Property4_DocumentEngineAndFallbackMetadata()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property4Seed);

        for (var i = 0; i < Property4Iterations; i++)
        {
            var extension = Property1Extensions[random.Next(Property1Extensions.Length)];
            var officeAvail = random.Next(2) == 1;
            var libreAvail = random.Next(2) == 1;
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];
            var primaryStatus = Property1PrimaryStatuses[random.Next(Property1PrimaryStatuses.Length)];

            // When a fallback fires, this flag decides whether the alternate engine succeeds (fallback
            // produced the PDF) or also fails (both-engines-failed path) so DocumentEngine on failure
            // can be proven to be the last-attempted (alternate) engine.
            var fallbackShouldFail = random.Next(2) == 1;

            // Reference eligibility predicate (independent of the production selector wiring); mirrors
            // Property 1 so the FakeSelector hands the pipeline a realistic ordered eligible set.
            var officeSupported = Property1OfficeWordExtensions.Contains(extension)
                || Property1OfficePowerPointExtensions.Contains(extension);
            var officeEligible = officeSupported && officeAvail;
            var libreEligible = libreAvail; // every Property1 extension is LibreOffice-supported.

            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = new List<ConversionEngineKind>(2);
            if (officeFirst)
            {
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
            }
            else
            {
                if (libreEligible) eligible.Add(ConversionEngineKind.LibreOffice);
                if (officeEligible) eligible.Add(ConversionEngineKind.MicrosoftOffice);
            }

            // Primary engine that SelectAsync would choose (null => selection fails, no engine runs).
            ConversionEngineKind? primary = preference switch
            {
                BatchConversionEnginePreference.MicrosoftOffice =>
                    officeEligible ? ConversionEngineKind.MicrosoftOffice : (ConversionEngineKind?)null,
                BatchConversionEnginePreference.LibreOffice =>
                    libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null,
                _ => officeEligible
                    ? ConversionEngineKind.MicrosoftOffice
                    : (libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null)
            };

            var primaryError = $"primary {primaryStatus} failure #{i}";
            var fallbackError = $"fallback Failed failure #{i}";

            // Primary returns primaryStatus; the alternate succeeds unless fallbackShouldFail, in which
            // case it returns a recoverable failure so the both-engines-failed path is taken.
            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(ConversionEngineKind kind)
                => (request, _) =>
                {
                    if (primary is not null && kind == primary.Value)
                    {
                        return primaryStatus == ConversionStatus.Succeeded
                            ? FakeConversion(request, kind)
                            : FakeFailure(request, kind, primaryStatus, primaryError);
                    }

                    return fallbackShouldFail
                        ? FakeFailure(request, kind, ConversionStatus.Failed, fallbackError)
                        : FakeConversion(request, kind);
                };

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedPrimary = primary;
            var capturedEligible = eligible.ToArray();
            var selector = new FakeSelector(
                _ => capturedPrimary is null
                    ? BatchEngineSelectionResult.Failed("No engine available.")
                    : BatchEngineSelectionResult.Selected(capturedPrimary.Value),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var inputPath = temp.WriteFile($"input_{i}{extension}", "doc");
            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = true
            };
            var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

            var context =
                $"seed={Property4Seed}, iteration={i}, ext={extension}, officeAvail={officeAvail}, " +
                $"libreAvail={libreAvail}, pref={preference}, primaryStatus={primaryStatus}, " +
                $"fallbackShouldFail={fallbackShouldFail}, " +
                $"primary={(primary is null ? "none" : primary.Value.ToString())}, " +
                $"eligible=[{string.Join(",", eligible)}]";

            var result = await pipeline.RunAsync(job, null, CancellationToken.None);
            var item = Assert.Single(result.Items);

            // Selection failed: no engine ran, the item failed, and no fallback metadata or engine is
            // recorded (Req 2.3 - no fallback was attempted).
            if (primary is null)
            {
                Assert.True(
                    office.CallCount == 0 && libre.CallCount == 0,
                    $"Expected no engine invocation when selection fails. " +
                    $"office={office.CallCount}, libre={libre.CallCount}. {context}");
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed when selection fails, got {item.Status}. {context}");
                Assert.True(
                    item.Fallback is null,
                    $"Expected null Fallback metadata when selection fails. {context}");
                Assert.True(
                    item.DocumentEngine is null,
                    $"Expected null DocumentEngine when selection fails, got {item.DocumentEngine}. {context}");
                continue;
            }

            var primaryFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            Assert.True(
                primaryFake.CallCount == 1,
                $"Expected primary engine to run exactly once, got {primaryFake.CallCount}. {context}");

            var primaryRecoverable = primaryStatus != ConversionStatus.Succeeded
                && RecoverableFailureStatuses.Contains(primaryStatus);
            var alternate = eligible.FirstOrDefault(e => e != primary.Value, primary.Value);
            var alternateExists = eligible.Any(e => e != primary.Value);
            var fallbackAttempted = primaryRecoverable && alternateExists;

            // Ground the "fallback attempted" expectation in the observed alternate-engine invocation.
            Assert.True(
                (fallbackFake.CallCount > 0) == fallbackAttempted,
                $"Fallback invocation mismatch. invoked={fallbackFake.CallCount > 0}, " +
                $"expected={fallbackAttempted} (recoverable={primaryRecoverable}, " +
                $"alternateExists={alternateExists}). {context}");

            if (!fallbackAttempted)
            {
                // No fallback was attempted: no fallback metadata, no fallback warning, and
                // DocumentEngine equals the only engine attempted (the primary), whether the primary
                // succeeded (producing engine) or failed (last engine attempted) - Req 5.5.
                Assert.True(
                    item.Fallback is null,
                    $"Expected null Fallback when no fallback was attempted, got non-null. {context}");
                Assert.False(
                    item.Warnings.Any(w => w.StartsWith("Fallback:", StringComparison.Ordinal)),
                    $"Expected no 'Fallback:' warning when no fallback was attempted. {context}");
                Assert.True(
                    item.DocumentEngine == primary.Value,
                    $"Expected DocumentEngine={primary.Value} (only engine attempted), " +
                    $"got {item.DocumentEngine}. {context}");

                if (primaryStatus == ConversionStatus.Succeeded)
                {
                    Assert.True(
                        item.Status == BatchConversionItemStatus.Succeeded,
                        $"Expected Succeeded on primary success, got {item.Status} ({item.ErrorMessage}). {context}");
                }
                else
                {
                    Assert.True(
                        item.Status == BatchConversionItemStatus.Failed,
                        $"Expected Failed on primary failure with no fallback, got {item.Status}. {context}");
                    Assert.True(
                        item.ErrorMessage == primaryError,
                        $"Expected ErrorMessage to equal the primary reason '{primaryError}', " +
                        $"got '{item.ErrorMessage}'. {context}");
                }

                continue;
            }

            // A fallback was attempted: it runs exactly once and the structured metadata is populated
            // regardless of the fallback outcome (Req 5.1, 5.4).
            Assert.True(
                fallbackFake.CallCount == 1,
                $"Expected fallback engine to run exactly once, got {fallbackFake.CallCount}. {context}");
            Assert.NotNull(item.Fallback);
            var info = item.Fallback!;
            Assert.True(
                info.PrimaryEngine == primary.Value,
                $"Expected Fallback.PrimaryEngine={primary.Value}, got {info.PrimaryEngine}. {context}");
            Assert.True(
                info.FallbackEngine == alternate,
                $"Expected Fallback.FallbackEngine={alternate}, got {info.FallbackEngine}. {context}");
            Assert.True(
                info.PrimaryFailureReason == primaryError,
                $"Expected Fallback.PrimaryFailureReason='{primaryError}', " +
                $"got '{info.PrimaryFailureReason}'. {context}");
            Assert.True(
                item.Warnings.Any(w => w.StartsWith("Fallback:", StringComparison.Ordinal)),
                $"Expected at least one 'Fallback:' warning when a fallback was attempted. {context}");

            if (!fallbackShouldFail)
            {
                // Fallback produced the PDF: the item succeeds and DocumentEngine equals the producing
                // (alternate) engine (Req 1.2, 5.2).
                Assert.True(
                    item.Status == BatchConversionItemStatus.Succeeded,
                    $"Expected Succeeded after fallback success, got {item.Status} ({item.ErrorMessage}). {context}");
                Assert.True(
                    item.DocumentEngine == alternate,
                    $"Expected DocumentEngine={alternate} (producing engine) after fallback success, " +
                    $"got {item.DocumentEngine}. {context}");
                Assert.True(
                    info.FallbackSucceeded,
                    $"Expected Fallback.FallbackSucceeded=true after fallback success. {context}");
            }
            else
            {
                // Both engines failed: DocumentEngine equals the last engine attempted (the alternate)
                // per Req 5.5, while ErrorMessage retains the PRIMARY failure reason (task 3.1 detail).
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed when both engines fail, got {item.Status}. {context}");
                Assert.True(
                    item.DocumentEngine == alternate,
                    $"Expected DocumentEngine={alternate} (last-attempted engine) when both engines fail, " +
                    $"got {item.DocumentEngine}. {context}");
                Assert.True(
                    item.ErrorMessage == primaryError,
                    $"Expected ErrorMessage to retain the primary reason '{primaryError}' when both " +
                    $"engines fail, got '{item.ErrorMessage}'. {context}");
                Assert.False(
                    info.FallbackSucceeded,
                    $"Expected Fallback.FallbackSucceeded=false when both engines fail. {context}");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Property 5: Setting OFF disables fallback.
    // Validates: Requirements 4.4
    //
    // Drives the REAL BatchConversionPipeline over the random matrix of
    // (primaryStatus, officeAvail, libreAvail, preference, extension) with EnableEngineFallback = FALSE
    // for EVERY iteration. The FakeSelector still hands the pipeline an ordered eligible set that WOULD
    // contain a distinct alternate engine, so the ONLY reason a fallback does not fire is the disabled
    // setting. The primary engine fake returns primaryStatus; the alternate engine fake would succeed
    // if it were ever invoked (so any setting-off violation would visibly flip Status/DocumentEngine).
    //
    // For every iteration where selection succeeds (primary is non-null) this asserts:
    //   * The fallback (alternate) engine is never invoked (CallCount == 0) - Req 4.4.
    //   * On a recoverable (or any non-cancellation) primary failure, the item Status == Failed and the
    //     final ErrorMessage equals the primary engine's ORIGINAL failure reason, unchanged - Req 4.4.
    //   * On primary success, the item Status == Succeeded with DocumentEngine == primary (sanity).
    //   * result.Fallback is null in every case (no fallback was attempted).
    //
    // Uses its own fixed seed (distinct from Properties 1-4) and >= 100 deterministic iterations, with
    // the seed + iteration recorded in every assertion message; hand-written fakes, no new PBT
    // dependency, CPU-only, no real Office/LibreOffice (per AGENTS.md). Reuses the task 3.2 fakes and
    // the shared Property1 matrix arrays verbatim. Cancellation is owned by Property 3, so the primary
    // status set (Property1PrimaryStatuses) excludes Cancelled.
    // ---------------------------------------------------------------------------------------------
    private const int Property5Iterations = 200;
    private const int Property5Seed = 20250127;

    [Fact]
    public async Task Property5_SettingOffDisablesFallback()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property5Seed);

        for (var i = 0; i < Property5Iterations; i++)
        {
            var extension = Property1Extensions[random.Next(Property1Extensions.Length)];
            var officeAvail = random.Next(2) == 1;
            var libreAvail = random.Next(2) == 1;
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];
            var primaryStatus = Property1PrimaryStatuses[random.Next(Property1PrimaryStatuses.Length)];

            // Reference eligibility predicate (independent of the production selector wiring); mirrors
            // Property 1 so the FakeSelector picks a realistic primary engine for the preference.
            var officeSupported = Property1OfficeWordExtensions.Contains(extension)
                || Property1OfficePowerPointExtensions.Contains(extension);
            var officeEligible = officeSupported && officeAvail;
            var libreEligible = libreAvail; // every Property1 extension is LibreOffice-supported.

            // Primary engine that SelectAsync would choose (null => selection fails, no engine runs).
            ConversionEngineKind? primary = preference switch
            {
                BatchConversionEnginePreference.MicrosoftOffice =>
                    officeEligible ? ConversionEngineKind.MicrosoftOffice : (ConversionEngineKind?)null,
                BatchConversionEnginePreference.LibreOffice =>
                    libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null,
                _ => officeEligible
                    ? ConversionEngineKind.MicrosoftOffice
                    : (libreEligible ? ConversionEngineKind.LibreOffice : (ConversionEngineKind?)null)
            };

            // Hand the selector an ordered eligible set that ALWAYS contains both engines (hence a
            // distinct alternate to whichever engine is primary). The pipeline never queries this set
            // when EnableEngineFallback is off, so configuring a full two-engine set makes the intent
            // explicit: the only reason fallback does not fire is the disabled setting (Req 4.4).
            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = officeFirst
                ? new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice }
                : new[] { ConversionEngineKind.LibreOffice, ConversionEngineKind.MicrosoftOffice };

            var primaryError = $"primary {primaryStatus} failure #{i}";

            // The primary engine returns primaryStatus; the alternate engine would succeed if invoked,
            // so a stray fallback would visibly flip Status/DocumentEngine and fail an assertion.
            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(ConversionEngineKind kind)
                => (request, _) =>
                    primary is not null && kind == primary.Value
                        ? (primaryStatus == ConversionStatus.Succeeded
                            ? FakeConversion(request, kind)
                            : FakeFailure(request, kind, primaryStatus, primaryError))
                        : FakeConversion(request, kind);

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedPrimary = primary;
            var capturedEligible = eligible;
            var selector = new FakeSelector(
                _ => capturedPrimary is null
                    ? BatchEngineSelectionResult.Failed("No engine available.")
                    : BatchEngineSelectionResult.Selected(capturedPrimary.Value),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var inputPath = temp.WriteFile($"input_{i}{extension}", "doc");
            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = false
            };
            var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

            var context =
                $"seed={Property5Seed}, iteration={i}, ext={extension}, officeAvail={officeAvail}, " +
                $"libreAvail={libreAvail}, pref={preference}, primaryStatus={primaryStatus}, " +
                $"primary={(primary is null ? "none" : primary.Value.ToString())}, " +
                $"eligible=[{string.Join(",", eligible)}]";

            var result = await pipeline.RunAsync(job, null, CancellationToken.None);
            var item = Assert.Single(result.Items);

            // Selection failed: no engine ran and the item failed without any fallback (Req 2.3); the
            // disabled-setting guarantees below are vacuous, so skip to the next iteration.
            if (primary is null)
            {
                Assert.True(
                    office.CallCount == 0 && libre.CallCount == 0,
                    $"Expected no engine invocation when selection fails. " +
                    $"office={office.CallCount}, libre={libre.CallCount}. {context}");
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed when selection fails, got {item.Status}. {context}");
                Assert.True(
                    item.Fallback is null,
                    $"Expected null Fallback metadata when selection fails. {context}");
                continue;
            }

            var primaryFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary.Value == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            // The primary engine always runs exactly once.
            Assert.True(
                primaryFake.CallCount == 1,
                $"Expected primary engine to run exactly once, got {primaryFake.CallCount}. {context}");

            // Core guarantee (Req 4.4): with the setting OFF the fallback (alternate) engine is NEVER
            // invoked, even though a distinct eligible alternate exists.
            Assert.True(
                fallbackFake.CallCount == 0,
                $"Expected fallback engine to never run when EnableEngineFallback is off, " +
                $"got {fallbackFake.CallCount}. {context}");

            // No fallback was attempted, so no fallback metadata is recorded on the item result.
            Assert.True(
                item.Fallback is null,
                $"Expected null Fallback metadata when EnableEngineFallback is off, got non-null. {context}");

            if (primaryStatus == ConversionStatus.Succeeded)
            {
                // Sanity: primary success still succeeds and DocumentEngine is the primary engine.
                Assert.True(
                    item.Status == BatchConversionItemStatus.Succeeded,
                    $"Expected Succeeded on primary success, got {item.Status} ({item.ErrorMessage}). {context}");
                Assert.True(
                    item.DocumentEngine == primary.Value,
                    $"Expected DocumentEngine={primary.Value} on primary success, got {item.DocumentEngine}. {context}");
            }
            else
            {
                // On a (recoverable or otherwise non-cancellation) primary failure the item fails and
                // the final ErrorMessage equals the primary engine's ORIGINAL reason, unchanged - the
                // disabled setting suppresses fallback and leaves the primary failure authoritative
                // (Req 4.4).
                Assert.True(
                    item.Status == BatchConversionItemStatus.Failed,
                    $"Expected Failed on primary failure with the setting off, got {item.Status}. {context}");
                Assert.True(
                    item.ErrorMessage == primaryError,
                    $"Expected ErrorMessage to equal the primary reason '{primaryError}', " +
                    $"got '{item.ErrorMessage}'. {context}");
                Assert.True(
                    item.DocumentEngine == primary.Value,
                    $"Expected DocumentEngine={primary.Value} (only engine attempted) on failure, " +
                    $"got {item.DocumentEngine}. {context}");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Property 6: Execution is deterministic and serialized.
    // Validates: Requirements 7.1, 7.3
    //
    // Drives the REAL BatchConversionPipeline over a random matrix of (extension, preference,
    // recoverableStatus, itemCount) engineered so that EVERY item exercises BOTH a primary and a
    // fallback engine call: both engines are eligible, EnableEngineFallback = true, the primary engine
    // returns a recoverable failure, and the distinct eligible alternate succeeds. Each item therefore
    // runs the primary engine once then the fallback engine once (two sequential engine executions),
    // and multi-item jobs (2-3 items) additionally exercise cross-item serialization.
    //
    // Concurrency observation: a single shared active-counter (guarded by a lock) is incremented at the
    // start of each engine behavior and decremented at the end, recording the maximum simultaneously
    // observed value. While the counter is held, the behavior spins briefly (CPU-only) so that any
    // concurrent engine execution would be observable as activeEngines > 1. The SAME counter instance
    // is shared by both the office and LibreOffice fake behaviors and reused across every item in the
    // run. Because the pipeline awaits the primary before the fallback within an item AND processes
    // items one at a time, the observed maximum concurrency MUST be 1 (Req 7.1).
    //
    // Concurrency limits: the job's configured MaxDocumentConversions / MaxImageRenderJobs are read
    // back from the constructed options after the run and asserted unchanged (Req 7.3) - the pipeline
    // must not raise the configured limits to perform fallback.
    //
    // Uses its own fixed seed (distinct from Properties 1-5) and >= 100 deterministic iterations, with
    // the seed + iteration recorded in every assertion message; a short CPU-only spin (no Thread.Sleep
    // timer granularity), hand-written fakes, no new PBT dependency, no real Office/LibreOffice (per
    // AGENTS.md). Reuses the task 3.2 fakes and the shared Property1 matrix arrays verbatim.
    // ---------------------------------------------------------------------------------------------
    private const int Property6Iterations = 120;
    private const int Property6Seed = 20250318;

    // The configured concurrency limits for Property 6 jobs; asserted unchanged after each run so the
    // pipeline is proven not to raise them when performing fallback (Req 7.3).
    private const int Property6MaxDocumentConversions = 1;
    private const int Property6MaxImageRenderJobs = 1;

    // Two-engine formats only: with both engines available a distinct eligible alternate always exists,
    // so a recoverable primary failure guarantees a fallback attempt and thus two engine executions
    // per item to observe serialization across.
    private static readonly string[] Property6TwoEngineExtensions =
        { ".doc", ".docx", ".rtf", ".ppt", ".pptx" };

    [Fact]
    public async Task Property6_ExecutionIsSerializedAndDeterministic()
    {
        using var temp = TempDirectory.Create();
        var random = new Random(Property6Seed);

        for (var i = 0; i < Property6Iterations; i++)
        {
            var extension = Property6TwoEngineExtensions[random.Next(Property6TwoEngineExtensions.Length)];
            var preference = Property1Preferences[random.Next(Property1Preferences.Length)];
            var recoverableStatus =
                RecoverableFailureStatuses[random.Next(RecoverableFailureStatuses.Length)];
            var itemCount = 2 + random.Next(2); // 2 or 3 items per job exercises cross-item serialization.

            // Both engines available => both eligible regardless of preference, guaranteeing a distinct
            // eligible alternate so the fallback path fires for every item.
            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var eligible = officeFirst
                ? new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice }
                : new[] { ConversionEngineKind.LibreOffice, ConversionEngineKind.MicrosoftOffice };

            var primary = officeFirst
                ? ConversionEngineKind.MicrosoftOffice
                : ConversionEngineKind.LibreOffice;
            var alternate = primary == ConversionEngineKind.MicrosoftOffice
                ? ConversionEngineKind.LibreOffice
                : ConversionEngineKind.MicrosoftOffice;

            var primaryError = $"primary {recoverableStatus} failure #{i}";

            // Single shared active-counter instance, captured by BOTH engine behaviors and reused for
            // every item in this iteration's run. If the two engines ever overlapped (within an item or
            // across items), activeEngines would exceed 1 and maxConcurrent would record it.
            var concurrencyLock = new object();
            var activeEngines = 0;
            var maxConcurrent = 0;

            ConversionResult Track(Func<ConversionResult> body)
            {
                lock (concurrencyLock)
                {
                    activeEngines++;
                    if (activeEngines > maxConcurrent)
                    {
                        maxConcurrent = activeEngines;
                    }
                }

                // Hold the active state briefly so any concurrent engine execution would be observable
                // as activeEngines > 1. A CPU-only spin avoids Thread.Sleep timer granularity and keeps
                // 120 iterations fast.
                Thread.SpinWait(20_000);

                try
                {
                    return body();
                }
                finally
                {
                    lock (concurrencyLock)
                    {
                        activeEngines--;
                    }
                }
            }

            // Primary returns the recoverable failure; the alternate succeeds, so every item runs the
            // primary once then the fallback once. Both behaviors funnel through the shared Track.
            Func<ConversionRequest, CancellationToken, ConversionResult> BehaviorFor(ConversionEngineKind kind)
                => (request, _) => Track(() =>
                    kind == primary
                        ? FakeFailure(request, kind, recoverableStatus, primaryError)
                        : FakeConversion(request, kind));

            var office = new FakeOfficeEngine(BehaviorFor(ConversionEngineKind.MicrosoftOffice));
            var libre = new FakeLibreOfficeEngine(BehaviorFor(ConversionEngineKind.LibreOffice));

            var capturedEligible = eligible;
            var selector = new FakeSelector(
                _ => BatchEngineSelectionResult.Selected(primary),
                _ => BatchEngineEligibilityResult.From(capturedEligible));

            var pipeline = Pipeline(selector, libre, office);

            var outputRoot = temp.CreateSubdirectory($"out_{i}");
            var inputPaths = new string[itemCount];
            for (var item = 0; item < itemCount; item++)
            {
                inputPaths[item] = temp.WriteFile($"input_{i}_{item}{extension}", "doc");
            }

            var options = new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = preference,
                EnableEngineFallback = true,
                MaxDocumentConversions = Property6MaxDocumentConversions,
                MaxImageRenderJobs = Property6MaxImageRenderJobs
            };
            var job = new BatchConversionJob(
                inputPaths.Select(path => new BatchConversionItem(path)).ToArray(),
                options);

            var context =
                $"seed={Property6Seed}, iteration={i}, ext={extension}, pref={preference}, " +
                $"recoverableStatus={recoverableStatus}, itemCount={itemCount}, primary={primary}, " +
                $"alternate={alternate}, eligible=[{string.Join(",", eligible)}]";

            var result = await pipeline.RunAsync(job, null, CancellationToken.None);

            // Sanity: the scenario actually exercised BOTH a primary and a fallback engine call for
            // every item (so the serialization observation is meaningful, not vacuous).
            var primaryFake = primary == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)office
                : libre;
            var fallbackFake = primary == ConversionEngineKind.MicrosoftOffice
                ? (ConfigurableConversionEngine)libre
                : office;

            Assert.True(
                primaryFake.CallCount == itemCount,
                $"Expected the primary engine to run once per item ({itemCount}), " +
                $"got {primaryFake.CallCount}. {context}");
            Assert.True(
                fallbackFake.CallCount == itemCount,
                $"Expected the fallback engine to run once per item ({itemCount}), " +
                $"got {fallbackFake.CallCount}. {context}");
            Assert.True(
                result.Items.Count == itemCount,
                $"Expected {itemCount} item results, got {result.Items.Count}. {context}");

            // Core guarantee (Req 7.1): the primary and fallback attempts execute sequentially - within
            // an item and across items - so the maximum number of concurrently executing engines
            // observed is exactly one.
            Assert.True(
                maxConcurrent == 1,
                $"Expected max observed concurrent engine executions == 1, got {maxConcurrent}. {context}");

            // The configured concurrency limits are unchanged after a run that triggered fallback for
            // every item (Req 7.3): the pipeline must not raise MaxDocumentConversions / MaxImageRenderJobs.
            Assert.True(
                options.MaxDocumentConversions == Property6MaxDocumentConversions,
                $"Expected MaxDocumentConversions to remain {Property6MaxDocumentConversions}, " +
                $"got {options.MaxDocumentConversions}. {context}");
            Assert.True(
                options.MaxImageRenderJobs == Property6MaxImageRenderJobs,
                $"Expected MaxImageRenderJobs to remain {Property6MaxImageRenderJobs}, " +
                $"got {options.MaxImageRenderJobs}. {context}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Example-based pipeline edge cases (task 3.8). These are concrete [Fact] unit tests (not seeded
    // property loops) that pin down individual fallback guarantees the property tests cover only in
    // aggregate. They reuse the task 3.2 fakes/helpers verbatim. CPU-only, no real Office/LibreOffice.
    // ---------------------------------------------------------------------------------------------

    // Builds the standard "primary fails recoverably, eligible distinct alternate succeeds" selector:
    // SelectAsync chooses Microsoft Office; GetEligibleEnginesAsync offers [MicrosoftOffice, LibreOffice]
    // so LibreOffice is the distinct eligible fallback.
    private static FakeSelector OfficePrimaryLibreFallbackSelector()
        => new(
            _ => BatchEngineSelectionResult.Selected(ConversionEngineKind.MicrosoftOffice),
            _ => BatchEngineEligibilityResult.From(new[]
            {
                ConversionEngineKind.MicrosoftOffice,
                ConversionEngineKind.LibreOffice
            }));

    // A primary (Office) engine fake that always returns a recoverable failure.
    private static FakeOfficeEngine RecoverableOfficePrimary(string reason = "primary failed")
        => new((request, _) =>
            FakeFailure(request, ConversionEngineKind.MicrosoftOffice, ConversionStatus.Failed, reason));

    // Req 6.2: the pipeline passes the SAME CancellationToken to the fallback engine that it passed to
    // the primary, and that token is the one supplied to RunAsync.
    [Fact]
    public async Task Pipeline_Fallback_PassesSameTokenToBothEngines()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = RecoverableOfficePrimary();
        var libre = new FakeLibreOfficeEngine(); // default success => fallback produces the PDF
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);
        using var cts = new CancellationTokenSource();

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            cts.Token);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Succeeded, item.Status);
        Assert.Equal(ConversionEngineKind.LibreOffice, item.DocumentEngine);

        // Both engines ran exactly once, and the fallback received the same token as the primary.
        Assert.Equal(1, office.CallCount);
        Assert.Equal(1, libre.CallCount);
        Assert.True(
            libre.LastToken.Equals(office.LastToken),
            "Fallback engine must receive the same CancellationToken as the primary engine.");
        Assert.True(
            libre.LastToken.Equals(cts.Token),
            "Both attempts must use the token supplied to RunAsync.");
    }

    // Req 7.2: primary and fallback attempts reuse the injected engine instances (no new engines are
    // created); each injected fake's CallCount increments exactly once.
    [Fact]
    public async Task Pipeline_Fallback_ReusesInjectedEngineInstances()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = RecoverableOfficePrimary();
        var libre = new FakeLibreOfficeEngine();
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Succeeded, item.Status);
        Assert.Equal(ConversionEngineKind.LibreOffice, item.DocumentEngine);

        // Exactly one primary attempt and one fallback attempt on the SAME injected instances.
        Assert.Equal(1, office.CallCount);
        Assert.Equal(1, libre.CallCount);
    }

    // Req 7.3: a run that triggers fallback does not change the configured concurrency limits.
    [Fact]
    public async Task Pipeline_Fallback_DoesNotChangeConcurrencyLimits()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = RecoverableOfficePrimary();
        var libre = new FakeLibreOfficeEngine();
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);

        var options = new BatchConversionOptions
        {
            OutputRoot = temp.CreateSubdirectory("out"),
            Target = BatchConversionTarget.Pdf,
            EnableEngineFallback = true,
            MaxDocumentConversions = 1,
            MaxImageRenderJobs = 1
        };
        var job = new BatchConversionJob(new[] { new BatchConversionItem(inputPath) }, options);

        var result = await pipeline.RunAsync(job, null, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Succeeded, item.Status);

        // The pipeline must not raise the configured limits to perform fallback.
        Assert.Equal(1, options.MaxDocumentConversions);
        Assert.Equal(1, options.MaxImageRenderJobs);
    }

    // Req 7.4: the item-scoped temp root is removed after a fallback run (best-effort cleanup leaves no
    // lingering subdirectory under %TEMP%/SnappyDocsConvert/Batch).
    [Fact]
    public async Task Pipeline_Fallback_RemovesItemTempRoot()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = RecoverableOfficePrimary();
        var libre = new FakeLibreOfficeEngine();
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);

        // Per-item temp roots are created under %TEMP%/SnappyDocsConvert/Batch/<guid>. Snapshot the
        // Batch directory's entries before and after the run and assert the run leaves no new lingering
        // subdirectory (robust to dirs created by other tests).
        var batchRoot = Path.Combine(Path.GetTempPath(), "SnappyDocsConvert", "Batch");
        HashSet<string> SnapshotBatchDirs() => Directory.Exists(batchRoot)
            ? Directory.GetDirectories(batchRoot).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var before = SnapshotBatchDirs();

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Succeeded, item.Status);
        // Sanity: the scenario actually triggered the fallback path.
        Assert.Equal(ConversionEngineKind.LibreOffice, item.DocumentEngine);

        var after = SnapshotBatchDirs();
        var leaked = after.Except(before).ToArray();
        Assert.True(
            leaked.Length == 0,
            $"Fallback run left lingering temp directories: {string.Join(", ", leaked)}");
    }

    // Req 6.4: a multi-item job cancelled during a fallback attempt marks the cancelled item and every
    // subsequent item Cancelled, consistent with the existing batch cancellation behavior.
    [Fact]
    public async Task Pipeline_Fallback_CancelledDuringFallbackCancelsRemainingItems()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WriteFile("one.docx", "doc");
        var second = temp.WriteFile("two.docx", "doc");
        var third = temp.WriteFile("three.docx", "doc");
        using var cts = new CancellationTokenSource();

        var office = RecoverableOfficePrimary();
        // The fallback engine cancels the shared token then throws OCE, modelling cancellation that
        // arrives mid-fallback during the first item.
        var libre = new FakeLibreOfficeEngine((_, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);

        var result = await pipeline.RunAsync(
            Job(new[] { first, second, third }, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            cts.Token);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(BatchConversionItemStatus.Cancelled, item.Status));

        // Only the first item exercised the engines; cancellation cascaded to the remaining items, so
        // they never invoked an engine.
        Assert.Equal(1, office.CallCount);
        Assert.Equal(1, libre.CallCount);
    }

    // Req 2.3: when the selector reports selection failure, neither engine is invoked and no fallback
    // is attempted; the item is Failed.
    [Fact]
    public async Task Pipeline_SelectionFailure_InvokesNoEngineAndNoFallback()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "doc");
        var office = new FakeOfficeEngine();
        var libre = new FakeLibreOfficeEngine();
        // Selection fails, yet the eligible set still offers both engines: proves the failure - not a
        // missing alternate - is what suppresses any engine invocation.
        var selector = new FakeSelector(
            _ => BatchEngineSelectionResult.Failed("No engine available."),
            _ => BatchEngineEligibilityResult.From(new[]
            {
                ConversionEngineKind.MicrosoftOffice,
                ConversionEngineKind.LibreOffice
            }));
        var pipeline = Pipeline(selector, libre, office);

        var result = await pipeline.RunAsync(
            Job(inputPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Failed, item.Status);
        Assert.Equal(0, office.CallCount);
        Assert.Equal(0, libre.CallCount);
        Assert.Null(item.Fallback);
    }

    // Req 2.4: a pre-engine guard (here, a missing input file) rejects the item as Failed before any
    // engine runs, and no fallback is attempted.
    [Fact]
    public async Task Pipeline_PreEngineGuard_MissingFile_InvokesNoEngineAndNoFallback()
    {
        using var temp = TempDirectory.Create();
        var missingPath = Path.Combine(temp.Path, "missing.docx");
        var office = new FakeOfficeEngine();
        var libre = new FakeLibreOfficeEngine();
        var pipeline = Pipeline(OfficePrimaryLibreFallbackSelector(), libre, office);

        var result = await pipeline.RunAsync(
            Job(missingPath, temp.CreateSubdirectory("out"), BatchConversionTarget.Pdf),
            null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BatchConversionItemStatus.Failed, item.Status);
        Assert.Contains("does not exist", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, office.CallCount);
        Assert.Equal(0, libre.CallCount);
        Assert.Null(item.Fallback);
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
        private readonly Func<string, BatchEngineEligibilityResult> _eligible;

        public FakeSelector(
            Func<string, BatchEngineSelectionResult> select,
            Func<string, BatchEngineEligibilityResult>? eligible = null)
        {
            _select = select;
            // Default to an empty eligible set so callers that do not opt into fallback behaviour
            // (the existing single-engine tests) never find an alternate engine.
            _eligible = eligible ?? (_ => BatchEngineEligibilityResult.Empty);
        }

        public Task<BatchEngineSelectionResult> SelectAsync(
            string inputPath,
            BatchConversionOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_select(inputPath));

        public Task<BatchEngineEligibilityResult> GetEligibleEnginesAsync(
            string inputPath,
            BatchConversionOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_eligible(inputPath));
    }

    /// <summary>
    /// Shared, configurable conversion-engine fake. Each call optionally runs a per-call
    /// <see cref="_behavior"/> delegate to control the returned <see cref="ConversionResult"/> (or to
    /// throw, e.g. <see cref="OperationCanceledException"/>); when no behavior is supplied it falls back
    /// to the default success result (a real non-empty PDF written into the request output directory),
    /// preserving the engines' original test behavior. Records <see cref="CallCount"/> and the
    /// <see cref="LastToken"/> received so cancellation/serialization properties can assert on them.
    /// </summary>
    private abstract class ConfigurableConversionEngine
    {
        private readonly ConversionEngineKind _engineKind;
        private readonly Func<ConversionRequest, CancellationToken, ConversionResult>? _behavior;

        protected ConfigurableConversionEngine(
            ConversionEngineKind engineKind,
            Func<ConversionRequest, CancellationToken, ConversionResult>? behavior)
        {
            _engineKind = engineKind;
            _behavior = behavior;
        }

        public int CallCount { get; private set; }

        public CancellationToken LastToken { get; private set; }

        protected Task<ConversionResult> InvokeAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastToken = cancellationToken;
            // Evaluating the behavior here lets a configured delegate throw (e.g. OCE); the throw
            // propagates synchronously to the awaiting caller exactly as a real engine would.
            var result = (_behavior ?? DefaultSuccess)(request, cancellationToken);
            return Task.FromResult(result);
        }

        private ConversionResult DefaultSuccess(ConversionRequest request, CancellationToken _)
            => FakeConversion(request, _engineKind);
    }

    private sealed class FakeLibreOfficeEngine : ConfigurableConversionEngine, ILibreOfficeConversionEngine
    {
        public FakeLibreOfficeEngine(
            Func<ConversionRequest, CancellationToken, ConversionResult>? behavior = null)
            : base(ConversionEngineKind.LibreOffice, behavior)
        {
        }

        public Task<ConversionResult> ConvertToPdfAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
            => InvokeAsync(request, cancellationToken);
    }

    private sealed class FakeOfficeEngine : ConfigurableConversionEngine, IOfficeComConversionEngine
    {
        public FakeOfficeEngine(
            Func<ConversionRequest, CancellationToken, ConversionResult>? behavior = null)
            : base(ConversionEngineKind.MicrosoftOffice, behavior)
        {
        }

        public Task<ConversionResult> ConvertToPdfAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
            => InvokeAsync(request, cancellationToken);
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

    // Builds a failure ConversionResult (no PDF) with a caller-chosen status and message so a
    // configured fake can model recoverable/non-recoverable per-file failures.
    private static ConversionResult FakeFailure(
        ConversionRequest request,
        ConversionEngineKind engineKind,
        ConversionStatus status,
        string errorMessage)
        => new()
        {
            Success = false,
            Status = status,
            InputPath = request.InputPath,
            OutputPdfPath = null,
            EngineKind = engineKind,
            ErrorMessage = errorMessage
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

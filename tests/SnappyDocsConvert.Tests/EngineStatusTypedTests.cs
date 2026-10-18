using SnappyDocsConvert.App.Localization;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.App.ViewModels;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Tests;

// STEP 5 typed engine status: availability states come from probes, display labels are
// derived from the states (never from localized text driving logic). Recheck integrations
// compare against the real registry/path providers so the assertions stay valid on any
// machine without pinning Word available or LibreOffice missing.
public sealed class EngineStatusTypedTests
{
    private static EngineAvailabilityState Map(bool available) => available
        ? EngineAvailabilityState.Available
        : EngineAvailabilityState.Missing;

    private static string ExpectedGuidance(
        EngineSetupState state,
        string? blockingReason,
        string recommendationsText) => state switch
    {
        EngineSetupState.Ready => recommendationsText,
        EngineSetupState.Blocked => blockingReason ?? "",
        _ => throw new InvalidOperationException("unexpected terminal setup state")
    };

    [Fact]
    public void InitialState_IsCheckingAndLabelsDeriveFromStates()
    {
        var status = new EngineStatusViewModel(new LocalizationService());

        Assert.Equal(EngineAvailabilityState.Checking, status.WordState);
        Assert.Equal(EngineAvailabilityState.Checking, status.PowerPointState);
        Assert.Equal(EngineAvailabilityState.Checking, status.LibreOfficeState);
        Assert.Equal(EngineAvailabilityState.Available, status.PdfRendererState);
        Assert.Equal(EngineSetupState.Checking, status.SetupState);
        // LocalizationService defaults to English before the shell applies the saved language.
        Assert.Equal("Checking", status.WordLabel);
        Assert.Equal("Checking", status.PowerPointLabel);
        Assert.Equal("Checking", status.LibreOfficeLabel);
        Assert.Equal("Checking engines...", status.GuidanceText);
    }

    // The ctor starts the first real recheck in the background; poll for a terminal
    // state instead of calling the async-void command (Invoke would start a second check).
    private static void WaitForRecheckEnd(MainWindowViewModel viewModel, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            var terminalSetup = viewModel.EngineStatus.SetupState
                is EngineSetupState.Ready
                    or EngineSetupState.Blocked
                    or EngineSetupState.Failed;
            var terminalWord = viewModel.EngineStatus.WordState != EngineAvailabilityState.Checking;
            if (terminalSetup && terminalWord)
            {
                return;
            }
            Thread.Sleep(50);
        }
        throw new TimeoutException("engine recheck did not reach a terminal state");
    }

    [Fact]
    public async Task Recheck_MapsRealProbeResultsIntoTypedStates()
    {
        var factory = new AppServiceFactory();
        var expectedOffice = await factory.CreateOfficeAvailabilityProvider()
            .GetOfficeAvailabilityAsync(CancellationToken.None);

        var viewModel = new MainWindowViewModel(factory);
        WaitForRecheckEnd(viewModel);

        Assert.Equal(Map(expectedOffice.WordAvailable), viewModel.EngineStatus.WordState);
        Assert.Equal(Map(expectedOffice.PowerPointAvailable), viewModel.EngineStatus.PowerPointState);
        Assert.Equal(EngineAvailabilityState.Available, viewModel.EngineStatus.PdfRendererState);
        Assert.Contains(
            viewModel.EngineStatus.LibreOfficeState,
            new[] { EngineAvailabilityState.Available, EngineAvailabilityState.Missing });
    }

    [Fact]
    public void Recheck_IsIdempotentAndEndsInTerminalSetupState()
    {
        var factory = new AppServiceFactory();
        var viewModel = new MainWindowViewModel(factory);
        WaitForRecheckEnd(viewModel);
        var wordAfterFirst = viewModel.EngineStatus.WordState;
        var libreAfterFirst = viewModel.EngineStatus.LibreOfficeState;

        viewModel.RecheckEnginesCommand.Execute(null);
        WaitForRecheckEnd(viewModel);

        Assert.Equal(wordAfterFirst, viewModel.EngineStatus.WordState);
        Assert.Equal(libreAfterFirst, viewModel.EngineStatus.LibreOfficeState);
        Assert.NotEqual(EngineSetupState.Checking, viewModel.EngineStatus.SetupState);
    }

    [Fact]
    public async Task Recheck_GuidanceMatchesSetupAdvisorData()
    {
        var factory = new AppServiceFactory();
        var advisor = factory.CreateEngineSetupAdvisor();
        var setup = await advisor
            .GetStatusAsync(EngineSetupMode.Auto, null, CancellationToken.None);
        var expectedText = setup.BlockingReason
            ?? string.Join(" ", setup.Recommendations.Select(recommendation => recommendation.Message));

        var viewModel = new MainWindowViewModel(factory);
        WaitForRecheckEnd(viewModel);

        var expectedState = string.IsNullOrWhiteSpace(setup.BlockingReason)
            ? EngineSetupState.Ready
            : EngineSetupState.Blocked;
        Assert.Equal(expectedState, viewModel.EngineStatus.SetupState);
        Assert.Equal(expectedText, viewModel.EngineStatus.GuidanceText);
    }

    [Fact]
    public void SetupStates_DriveBlockingFailedAndReadyGuidance()
    {
        var status = new EngineStatusViewModel(new LocalizationService());

        status.SetSetupChecking();
        Assert.Equal(EngineSetupState.Checking, status.SetupState);
        Assert.Equal("Checking engines...", status.GuidanceText);

        status.SetSetupResult(null, "");
        Assert.Equal(EngineSetupState.Ready, status.SetupState);
        Assert.Equal("", status.GuidanceText);

        status.SetSetupResult("LibreOffice not found", "install LibreOffice");
        Assert.Equal(EngineSetupState.Blocked, status.SetupState);
        Assert.Equal("LibreOffice not found", status.GuidanceText);

        status.SetSetupFailed("boom");
        Assert.Equal(EngineSetupState.Failed, status.SetupState);
        Assert.Equal("Engine check failed: boom", status.GuidanceText);
    }

    [Fact]
    public void Labels_RederiveWhenLanguageChangesWithoutRecheck()
    {
        var localization = new LocalizationService();
        var status = new EngineStatusViewModel(localization);

        status.WordState = EngineAvailabilityState.Available;
        status.PowerPointState = EngineAvailabilityState.Missing;
        status.LibreOfficeState = EngineAvailabilityState.Checking;

        Assert.Equal("Available", status.WordLabel);
        Assert.Equal("Missing", status.PowerPointLabel);
        Assert.Equal("Checking", status.LibreOfficeLabel);

        localization.SetLanguage(Language.Vietnamese);

        Assert.Equal("Có sẵn", status.WordLabel);
        Assert.Equal("Thiếu", status.PowerPointLabel);
        Assert.Equal("Đang kiểm tra", status.LibreOfficeLabel);
        Assert.Equal("Đang kiểm tra engine...", status.GuidanceText);

        localization.SetLanguage(Language.English);

        Assert.Equal("Available", status.WordLabel);
        Assert.Equal("Missing", status.PowerPointLabel);
        Assert.Equal("Checking", status.LibreOfficeLabel);
    }

    [Fact]
    public void Recheck_DoesNotBreakConvertQueueOrProgress()
    {
        var factory = new AppServiceFactory();
        var viewModel = new MainWindowViewModel(factory);
        // Isolated per-test folder; deleting must never touch sibling tests' dirs.
        var directory = Path.Combine(
            Path.GetTempPath(), "kmb-engine-status-tests", Guid.NewGuid().ToString("N"));
        var pdfPath = Path.Combine(directory, "probe.pdf");
        Directory.CreateDirectory(directory);
        File.WriteAllText(pdfPath, "%PDF-1.4 probe");

        try
        {
            viewModel.Convert.AddPaths(new[] { pdfPath });
            Assert.Single(viewModel.Convert.Queue);

            viewModel.RecheckEnginesCommand.Execute(null);
            WaitForRecheckEnd(viewModel);

            Assert.Single(viewModel.Convert.Queue);
            Assert.Equal(100.0, ConvertPageViewModel.ComputeOverallPercent(
                BatchConversionItemStatus.Succeeded, 2, 3));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

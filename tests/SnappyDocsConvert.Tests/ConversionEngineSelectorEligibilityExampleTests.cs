using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Example-based unit tests for <see cref="ConversionEngineSelector.GetEligibleEnginesAsync"/>.
/// Validates: Requirements 3.1, 3.2.
///
/// These complement the property test in <see cref="ConversionEngineSelectorEligibilityTests"/> by
/// pinning down the specific, named eligibility examples called out in the design's testing strategy:
///   - LibreOffice-only formats (.odt/.odp) yield exactly [LibreOffice] (no fallback engine);
///   - .docx with only Office available yields exactly [MicrosoftOffice];
///   - .pptx with both engines available (Auto preference) yields [MicrosoftOffice, LibreOffice];
///   - .pdf (no engine needed) and an unsupported extension yield an empty set.
///
/// Uses hand-written availability fakes and the real selector; CPU-only, no real Office/LibreOffice.
/// </summary>
public sealed class ConversionEngineSelectorEligibilityExampleTests
{
    [Theory]
    [InlineData(".odt")]
    [InlineData(".odp")]
    public async Task LibreOfficeOnlyFormats_WithLibreOfficeAvailable_YieldExactlyLibreOffice(string extension)
    {
        // LibreOffice available; Office availability is irrelevant because Office cannot handle these.
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync($"input{extension}", options, CancellationToken.None);

        Assert.Equal(new[] { ConversionEngineKind.LibreOffice }, result.EligibleEngines);
    }

    [Fact]
    public async Task Docx_WithOfficeOnly_YieldsExactlyMicrosoftOffice()
    {
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: false);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync("input.docx", options, CancellationToken.None);

        Assert.Equal(new[] { ConversionEngineKind.MicrosoftOffice }, result.EligibleEngines);
    }

    [Fact]
    public async Task Pptx_WithBothEngines_AndAutoPreference_YieldsOfficeThenLibreOffice()
    {
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync("input.pptx", options, CancellationToken.None);

        Assert.Equal(
            new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice },
            result.EligibleEngines);
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".xyz")]
    public async Task PdfAndUnsupportedExtensions_YieldEmptySet(string extension)
    {
        // Even with both engines fully available, .pdf needs no engine and .xyz is unsupported.
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync($"input{extension}", options, CancellationToken.None);

        Assert.Empty(result.EligibleEngines);
    }

    [Fact]
    public async Task Xlsx_WithOfficeExcel_YieldsMicrosoftOffice()
    {
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: false, canExcel: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync("book.xlsx", options, CancellationToken.None);

        Assert.Equal(new[] { ConversionEngineKind.MicrosoftOffice }, result.EligibleEngines);
    }

    [Fact]
    public async Task Xlsx_WithBothEngines_YieldsOfficeThenLibreOffice()
    {
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: true, canExcel: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync("book.xlsx", options, CancellationToken.None);

        Assert.Equal(
            new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice },
            result.EligibleEngines);
    }

    [Fact]
    public async Task Ods_YieldsExactlyLibreOffice()
    {
        var selector = CreateSelector(canWord: true, canPowerPoint: true, libreAvailable: true, canExcel: true);
        var options = NewOptions(BatchConversionEnginePreference.Auto);

        var result = await selector.GetEligibleEnginesAsync("sheet.ods", options, CancellationToken.None);

        Assert.Equal(new[] { ConversionEngineKind.LibreOffice }, result.EligibleEngines);
    }

    private static BatchConversionOptions NewOptions(BatchConversionEnginePreference preference)
        => new()
        {
            OutputRoot = "out",
            EnginePreference = preference
        };

    private static ConversionEngineSelector CreateSelector(
        bool canWord,
        bool canPowerPoint,
        bool libreAvailable,
        bool canExcel = false)
    {
        var officeAvailability = new OfficeAvailability(
            WordAvailable: canWord,
            PowerPointAvailable: canPowerPoint,
            CanConvertWordDocuments: canWord,
            CanConvertPowerPointPresentations: canPowerPoint,
            Reasons: Array.Empty<string>(),
            Recommendations: Array.Empty<EngineSetupRecommendation>(),
            VersionSummary: null,
            ExcelAvailable: canExcel,
            CanConvertExcelWorkbooks: canExcel);

        var libreAvailability = libreAvailable
            ? EngineAvailability.Available("soffice")
            : EngineAvailability.Unavailable("LibreOffice missing.");

        return new ConversionEngineSelector(
            new FakeMicrosoftOfficeAvailabilityProvider(officeAvailability),
            new FakeLibreOfficeLocator(libreAvailability));
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
}

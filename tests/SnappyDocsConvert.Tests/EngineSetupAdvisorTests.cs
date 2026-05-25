using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;

namespace SnappyDocsConvert.Tests;

public sealed class EngineSetupAdvisorTests
{
    [Fact]
    public void NoOfficeNoLibreOffice_UserHasNoOffice_RecommendsOfficialLibreOfficeDownload()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Unavailable("LibreOffice missing."),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.IDoNotHaveMicrosoftOffice);

        Assert.False(status.CanConvertOfficeDocuments);
        Assert.Equal("LibreOffice is required when Microsoft Office is not available.", status.BlockingReason);
        Assert.Contains(FlattenActions(status), action =>
            action.Kind == EngineSetupActionKind.OpenUrl &&
            action.Target == KnownExternalToolLinks.LibreOfficeDownload.OfficialUrl);
    }

    [Fact]
    public void NoOfficeLibreOfficeAvailable_UserHasNoOffice_CanConvertViaLibreOffice()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Available("soffice.com"),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.IDoNotHaveMicrosoftOffice);

        Assert.True(status.CanConvertOfficeDocuments);
        Assert.Equal(ConversionEngineKind.LibreOffice, status.PreferredEngine);
        Assert.True(status.LibreOfficeAvailable);
    }

    [Fact]
    public void OfficeAvailable_UserHasOffice_CanConvertViaOffice()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Unavailable("LibreOffice missing."),
            EngineAvailability.Available("Office COM"),
            EngineSetupMode.IHaveMicrosoftOffice);

        Assert.True(status.CanConvertOfficeDocuments);
        Assert.Equal(ConversionEngineKind.MicrosoftOffice, status.PreferredEngine);
        Assert.True(status.MicrosoftOfficeAvailable);
    }

    [Fact]
    public void Auto_OfficeAvailable_PrefersOffice()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Available("soffice.com"),
            EngineAvailability.Available("Office COM"),
            EngineSetupMode.Auto);

        Assert.True(status.CanConvertOfficeDocuments);
        Assert.Equal(ConversionEngineKind.MicrosoftOffice, status.PreferredEngine);
    }

    [Fact]
    public void Auto_NoOfficeLibreOfficeAvailable_FallsBackToLibreOffice()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Available("soffice.com"),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.Auto);

        Assert.True(status.CanConvertOfficeDocuments);
        Assert.Equal(ConversionEngineKind.LibreOffice, status.PreferredEngine);
    }

    [Fact]
    public void Auto_NoEngines_ReturnsBlockingReasonAndSetupActions()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Unavailable("LibreOffice missing."),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.Auto);

        Assert.False(status.CanConvertOfficeDocuments);
        Assert.Equal("No local Office conversion engine is available.", status.BlockingReason);
        Assert.Contains(FlattenActions(status), action => action.Kind == EngineSetupActionKind.ChooseExecutable);
        Assert.Contains(FlattenActions(status), action => action.Kind == EngineSetupActionKind.Recheck);
    }

    [Fact]
    public async Task CustomLibreOfficeValidPath_CanConvertViaLibreOffice()
    {
        var advisor = CreateAdvisor(
            new FakeLibreOfficeLocator(options => options.ExecutablePath == "valid"
                ? EngineAvailability.Available("valid")
                : EngineAvailability.Unavailable("invalid")),
            new FakeMicrosoftOfficeAvailabilityProvider(EngineAvailability.Unavailable("Office missing.")));

        var status = await advisor.GetStatusAsync(
            EngineSetupMode.CustomLibreOfficePath,
            "valid",
            CancellationToken.None);

        Assert.True(status.CanConvertOfficeDocuments);
        Assert.Equal(ConversionEngineKind.LibreOffice, status.PreferredEngine);
    }

    [Fact]
    public async Task CustomLibreOfficeInvalidPath_ReturnsClearErrorAndRecoveryActions()
    {
        var advisor = CreateAdvisor(
            new FakeLibreOfficeLocator(_ => EngineAvailability.Unavailable("Selected path is not soffice.")),
            new FakeMicrosoftOfficeAvailabilityProvider(EngineAvailability.Unavailable("Office missing.")));

        var status = await advisor.GetStatusAsync(
            EngineSetupMode.CustomLibreOfficePath,
            "bad",
            CancellationToken.None);

        Assert.False(status.CanConvertOfficeDocuments);
        Assert.Contains("Selected path is not soffice", status.BlockingReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(FlattenActions(status), action => action.Kind == EngineSetupActionKind.ChooseExecutable);
        Assert.Contains(FlattenActions(status), action => action.Kind == EngineSetupActionKind.Recheck);
    }

    [Fact]
    public void Recommendations_IncludeOfficialLibreOfficeUrl()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Unavailable("LibreOffice missing."),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.UseLibreOffice);

        Assert.Contains(FlattenActions(status), action =>
            action.Target == "https://www.libreoffice.org/download/download-libreoffice/");
    }

    [Fact]
    public void NoFakeSuccess_WhenNoEngineAvailable()
    {
        var advisor = CreateAdvisor();

        var status = advisor.Advise(
            EngineAvailability.Unavailable("LibreOffice missing."),
            EngineAvailability.Unavailable("Office missing."),
            EngineSetupMode.Auto);

        Assert.False(status.CanConvertOfficeDocuments);
        Assert.Null(status.PreferredEngine);
    }

    private static EngineSetupAdvisor CreateAdvisor(
        ILibreOfficeLocator? libreOfficeLocator = null,
        IMicrosoftOfficeAvailabilityProvider? microsoftOfficeAvailabilityProvider = null)
        => new(
            libreOfficeLocator ?? new FakeLibreOfficeLocator(_ => EngineAvailability.Unavailable("LibreOffice missing.")),
            microsoftOfficeAvailabilityProvider ?? new FakeMicrosoftOfficeAvailabilityProvider(
                EngineAvailability.Unavailable("Office missing.")));

    private static IEnumerable<EngineSetupAction> FlattenActions(EngineSetupStatus status)
        => status.Recommendations.SelectMany(recommendation => recommendation.Actions);

    private sealed class FakeLibreOfficeLocator : ILibreOfficeLocator
    {
        private readonly Func<LibreOfficeOptions, EngineAvailability> _handler;

        public FakeLibreOfficeLocator(Func<LibreOfficeOptions, EngineAvailability> handler)
        {
            _handler = handler;
        }

        public Task<EngineAvailability> LocateAsync(
            LibreOfficeOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_handler(options));
    }

    private sealed class FakeMicrosoftOfficeAvailabilityProvider : IMicrosoftOfficeAvailabilityProvider
    {
        private readonly EngineAvailability _availability;

        public FakeMicrosoftOfficeAvailabilityProvider(EngineAvailability availability)
        {
            _availability = availability;
        }

        public Task<EngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(_availability);

        public Task<OfficeAvailability> GetOfficeAvailabilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(new OfficeAvailability(
                _availability.IsAvailable,
                _availability.IsAvailable,
                _availability.IsAvailable,
                _availability.IsAvailable,
                _availability.IsAvailable ? Array.Empty<string>() : new[] { _availability.Reason ?? "Office missing." },
                Array.Empty<EngineSetupRecommendation>(),
                _availability.Version));
    }
}

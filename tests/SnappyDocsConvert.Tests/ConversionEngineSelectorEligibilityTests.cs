using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Property 7: Engine eligibility set is correct and deterministic.
/// Validates: Requirements 3.1, 3.2, 3.4.
///
/// Drives the real <see cref="ConversionEngineSelector.GetEligibleEnginesAsync"/> over the
/// cross-product of (extension, office availability, LibreOffice availability, engine preference)
/// and asserts that:
///   - Microsoft Office is in the set iff the extension is Office-supported and the matching Office
///     app is available (CanConvertWordDocuments for .doc/.docx/.rtf, CanConvertPowerPointPresentations
///     for .ppt/.pptx);
///   - LibreOffice is in the set iff the extension is LibreOffice-supported and LibreOffice is available;
///   - .pdf and unsupported extensions yield an empty set;
///   - single-engine formats (.odt/.odp) yield exactly [LibreOffice] (no fallback engine) when LO is
///     available, otherwise empty;
///   - ordering encodes preference (Office first unless preference is LibreOffice);
///   - the returned set is de-duplicated; and
///   - two successive calls with the same inputs return identical ordered sets (determinism).
///
/// Implemented as a deterministic, seeded xUnit loop (no new PBT dependency, per AGENTS.md), CPU-only,
/// with hand-written availability fakes and no real Office/LibreOffice.
/// </summary>
public sealed class ConversionEngineSelectorEligibilityTests
{
    private const int Iterations = 200;
    private const int Seed = 20240222;

    private static readonly string[] OfficeWordExtensions = { ".doc", ".docx", ".rtf" };
    private static readonly string[] OfficePowerPointExtensions = { ".ppt", ".pptx" };
    private static readonly string[] LibreOfficeExtensions =
        { ".doc", ".docx", ".rtf", ".odt", ".ppt", ".pptx", ".odp" };

    // Full input space of extensions: every supported format, .pdf (no engine needed), and an
    // unsupported extension.
    private static readonly string[] Extensions =
        { ".doc", ".docx", ".rtf", ".ppt", ".pptx", ".odt", ".odp", ".pdf", ".xyz" };

    private static readonly BatchConversionEnginePreference[] Preferences =
    {
        BatchConversionEnginePreference.Auto,
        BatchConversionEnginePreference.MicrosoftOffice,
        BatchConversionEnginePreference.LibreOffice
    };

    [Fact]
    public async Task GetEligibleEnginesAsync_MembershipMatchesPredicate_AndIsDeterministic()
    {
        var random = new Random(Seed);

        for (var i = 0; i < Iterations; i++)
        {
            var extension = Extensions[random.Next(Extensions.Length)];
            var canWord = random.Next(2) == 1;
            var canPowerPoint = random.Next(2) == 1;
            var libreAvailable = random.Next(2) == 1;
            var preference = Preferences[random.Next(Preferences.Length)];

            var context =
                $"seed={Seed}, iteration={i}, ext={extension}, canWord={canWord}, " +
                $"canPpt={canPowerPoint}, libre={libreAvailable}, pref={preference}";

            var selector = CreateSelector(canWord, canPowerPoint, libreAvailable);
            var options = new BatchConversionOptions
            {
                OutputRoot = "out",
                EnginePreference = preference
            };
            var inputPath = $"input{extension}";

            var result = await selector.GetEligibleEnginesAsync(inputPath, options, CancellationToken.None);
            var actual = result.EligibleEngines;

            // Reference eligibility predicate (independent reimplementation of the spec rules).
            var officeSupported = OfficeWordExtensions.Contains(extension)
                || OfficePowerPointExtensions.Contains(extension);
            var officeEligible =
                (OfficeWordExtensions.Contains(extension) && canWord)
                || (OfficePowerPointExtensions.Contains(extension) && canPowerPoint);
            var libreEligible = LibreOfficeExtensions.Contains(extension) && libreAvailable;

            // Build the expected ordered, de-duplicated set from the predicate.
            var officeFirst = preference != BatchConversionEnginePreference.LibreOffice;
            var expected = new List<ConversionEngineKind>(2);
            if (officeFirst)
            {
                if (officeEligible) expected.Add(ConversionEngineKind.MicrosoftOffice);
                if (libreEligible) expected.Add(ConversionEngineKind.LibreOffice);
            }
            else
            {
                if (libreEligible) expected.Add(ConversionEngineKind.LibreOffice);
                if (officeEligible) expected.Add(ConversionEngineKind.MicrosoftOffice);
            }

            // Membership + ordering must match the predicate exactly (Req 3.1, 3.2, 3.4).
            Assert.True(
                expected.SequenceEqual(actual),
                $"Eligible set mismatch. expected=[{string.Join(",", expected)}], " +
                $"actual=[{string.Join(",", actual)}]. {context}");

            // De-duplication invariant (Req 3.4).
            Assert.True(
                actual.Distinct().Count() == actual.Count,
                $"Eligible set contained duplicates: [{string.Join(",", actual)}]. {context}");

            // .pdf and unsupported extensions yield no eligible engine (Req 3.4).
            if (extension is ".pdf" or ".xyz")
            {
                Assert.True(
                    actual.Count == 0,
                    $"Expected empty eligible set for '{extension}', got [{string.Join(",", actual)}]. {context}");
            }

            // Single-engine formats never produce a fallback engine (Req 3.4): Office cannot handle
            // .odt/.odp, so the set is exactly [LibreOffice] when LO is available, else empty.
            if (extension is ".odt" or ".odp")
            {
                Assert.DoesNotContain(ConversionEngineKind.MicrosoftOffice, actual);
                if (libreAvailable)
                {
                    Assert.Equal(new[] { ConversionEngineKind.LibreOffice }, actual);
                }
                else
                {
                    Assert.Empty(actual);
                }
            }

            // Office membership iff Office-supported AND matching app available (Req 3.1).
            Assert.True(
                actual.Contains(ConversionEngineKind.MicrosoftOffice) == (officeSupported && officeEligible),
                $"Office membership mismatch. inSet={actual.Contains(ConversionEngineKind.MicrosoftOffice)}, " +
                $"expected={officeSupported && officeEligible}. {context}");

            // LibreOffice membership iff LO-supported AND LO available (Req 3.2).
            Assert.True(
                actual.Contains(ConversionEngineKind.LibreOffice) == libreEligible,
                $"LibreOffice membership mismatch. inSet={actual.Contains(ConversionEngineKind.LibreOffice)}, " +
                $"expected={libreEligible}. {context}");

            // Determinism: a second call with identical inputs returns an identical ordered set (Req 3.4).
            var second = await selector.GetEligibleEnginesAsync(inputPath, options, CancellationToken.None);
            Assert.True(
                actual.SequenceEqual(second.EligibleEngines),
                $"Non-deterministic result. first=[{string.Join(",", actual)}], " +
                $"second=[{string.Join(",", second.EligibleEngines)}]. {context}");
        }
    }

    private static ConversionEngineSelector CreateSelector(
        bool canWord,
        bool canPowerPoint,
        bool libreAvailable)
    {
        var officeAvailability = new OfficeAvailability(
            WordAvailable: canWord,
            PowerPointAvailable: canPowerPoint,
            CanConvertWordDocuments: canWord,
            CanConvertPowerPointPresentations: canPowerPoint,
            Reasons: Array.Empty<string>(),
            Recommendations: Array.Empty<EngineSetupRecommendation>(),
            VersionSummary: null);

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

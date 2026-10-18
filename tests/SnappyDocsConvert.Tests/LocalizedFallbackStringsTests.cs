using System.Globalization;
using SnappyDocsConvert.App.Localization;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Property 8: Localized fallback strings resolve and preserve runtime values.
/// Validates: Requirements 8.1, 8.2, 8.3, 8.4.
///
/// Two parts, both CPU-only with no new PBT dependency (per AGENTS.md):
///   1. For every new fallback key (fallback-attempt, fallback-success, both-engines-failed, and the
///      EnableEngineFallback setting label) and both languages (English, Vietnamese),
///      <see cref="LocalizedStrings.Get"/> returns a non-empty value distinct from the raw key.
///   2. A deterministic, seeded xUnit loop (fixed seed recorded in assertion messages, >= 100
///      iterations) draws varied (fileName, engineName, reason) runtime values, composes the
///      localized FallbackAttempt/FallbackSuccess/BothEnginesFailed messages via string.Format over
///      the localized template, and asserts each runtime value that appears in that template is
///      present verbatim in the composed message, for both English and Vietnamese.
/// </summary>
public sealed class LocalizedFallbackStringsTests
{
    private const int Iterations = 200;
    private const int Seed = 20240308;

    private static readonly Language[] Languages = { Language.English, Language.Vietnamese };

    private static readonly string[] FallbackKeys =
    {
        "EnableEngineFallback",
        "FallbackAttempt",
        "FallbackSuccess",
        "BothEnginesFailed"
    };

    // Varied runtime values. None contain '{' or '}' so they cannot perturb composite formatting.
    private static readonly string[] FileNames =
    {
        "report.docx",
        "Báo cáo quý 1.pptx",
        "my file (final).rtf",
        "résumé.doc",
        "deck 2024.ppt",
        "annual-summary.odt"
    };

    private static readonly string[] EngineNames =
    {
        "Microsoft Office",
        "LibreOffice",
        LocalizedStrings.Get(Language.English, "EngineMicrosoftOffice"),
        LocalizedStrings.Get(Language.English, "EngineLibreOffice"),
        LocalizedStrings.Get(Language.Vietnamese, "EngineMicrosoftOffice"),
        LocalizedStrings.Get(Language.Vietnamese, "EngineLibreOffice")
    };

    private static readonly string[] Reasons =
    {
        "Word returned no document instance.",
        "PowerPoint export failed.",
        "COM error 0x80004005",
        "Timed out after 120s",
        "Output file missing."
    };

    [Fact]
    public void FallbackKeys_Resolve_NonEmpty_AndDistinctFromKey_InBothLanguages()
    {
        foreach (var language in Languages)
        {
            foreach (var key in FallbackKeys)
            {
                var value = LocalizedStrings.Get(language, key);

                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"Key '{key}' resolved to empty/whitespace for {language}.");
                Assert.NotEqual(key, value);
            }
        }
    }

    [Fact]
    public void ComposedFallbackMessages_PreserveRuntimeValues_Verbatim_InBothLanguages()
    {
        var random = new Random(Seed);

        for (var i = 0; i < Iterations; i++)
        {
            var fileName = FileNames[random.Next(FileNames.Length)];
            var primaryEngine = EngineNames[random.Next(EngineNames.Length)];
            var fallbackEngine = EngineNames[random.Next(EngineNames.Length)];
            var reason = Reasons[random.Next(Reasons.Length)];

            foreach (var language in Languages)
            {
                var context =
                    $"seed={Seed}, iteration={i}, lang={language}, file='{fileName}', " +
                    $"primary='{primaryEngine}', fallback='{fallbackEngine}', reason='{reason}'";

                // FallbackAttempt: {0}=file, {1}=primary engine, {2}=reason, {3}=fallback engine.
                var attemptTemplate = LocalizedStrings.Get(language, "FallbackAttempt");
                var attempt = string.Format(
                    CultureInfo.InvariantCulture, attemptTemplate, fileName, primaryEngine, reason, fallbackEngine);

                AssertContainsVerbatim(attempt, fileName, "FallbackAttempt.fileName", context);
                AssertContainsVerbatim(attempt, primaryEngine, "FallbackAttempt.primaryEngine", context);
                AssertContainsVerbatim(attempt, reason, "FallbackAttempt.reason", context);
                AssertContainsVerbatim(attempt, fallbackEngine, "FallbackAttempt.fallbackEngine", context);

                // FallbackSuccess: {0}=file, {1}=engine that produced the PDF.
                var successTemplate = LocalizedStrings.Get(language, "FallbackSuccess");
                var success = string.Format(
                    CultureInfo.InvariantCulture, successTemplate, fileName, fallbackEngine);

                AssertContainsVerbatim(success, fileName, "FallbackSuccess.fileName", context);
                AssertContainsVerbatim(success, fallbackEngine, "FallbackSuccess.engine", context);

                // BothEnginesFailed: {0}=file.
                var bothTemplate = LocalizedStrings.Get(language, "BothEnginesFailed");
                var both = string.Format(CultureInfo.InvariantCulture, bothTemplate, fileName);

                AssertContainsVerbatim(both, fileName, "BothEnginesFailed.fileName", context);
            }
        }
    }

    private static void AssertContainsVerbatim(string composed, string runtimeValue, string label, string context)
    {
        Assert.True(
            composed.Contains(runtimeValue, StringComparison.Ordinal),
            $"Composed message missing verbatim runtime value for {label}. " +
            $"value='{runtimeValue}', composed='{composed}'. {context}");
    }
}

using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Tests;

public sealed class BatchFallbackModelTests
{
    [Fact]
    public void BatchConversionOptions_EnableEngineFallback_defaults_to_true()
    {
        var options = new BatchConversionOptions { OutputRoot = "out" };

        Assert.True(options.EnableEngineFallback);
    }

    [Fact]
    public void BatchEngineEligibilityResult_Empty_has_no_eligible_engines()
    {
        Assert.Empty(BatchEngineEligibilityResult.Empty.EligibleEngines);
    }

    [Fact]
    public void BatchEngineEligibilityResult_From_deduplicates_preserving_order()
    {
        var result = BatchEngineEligibilityResult.From(new[]
        {
            ConversionEngineKind.MicrosoftOffice,
            ConversionEngineKind.LibreOffice,
            ConversionEngineKind.MicrosoftOffice,
            ConversionEngineKind.LibreOffice
        });

        Assert.Equal(
            new[] { ConversionEngineKind.MicrosoftOffice, ConversionEngineKind.LibreOffice },
            result.EligibleEngines);
    }

    [Fact]
    public void BatchEngineEligibilityResult_From_preserves_libreoffice_first_order()
    {
        var result = BatchEngineEligibilityResult.From(new[]
        {
            ConversionEngineKind.LibreOffice,
            ConversionEngineKind.MicrosoftOffice,
            ConversionEngineKind.LibreOffice
        });

        Assert.Equal(
            new[] { ConversionEngineKind.LibreOffice, ConversionEngineKind.MicrosoftOffice },
            result.EligibleEngines);
    }
}

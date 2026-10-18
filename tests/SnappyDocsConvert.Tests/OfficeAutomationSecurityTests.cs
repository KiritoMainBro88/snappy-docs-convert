using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Regression tests for the per-file AutomationSecurity envelope (reflection-based, so it runs on
/// plain stubs without Office). Microsoft guidance: save and restore the automation security state
/// around file opens — including on shared instances and even when the operation fails.
/// </summary>
public sealed class OfficeAutomationSecurityTests
{
    private const int ForceDisable = 3;

    public sealed class SecurityApp
    {
        public int AutomationSecurity { get; set; } = 1;

        public int SetCount { get; private set; }
    }

    private sealed class OpaqueApp
    {
        // No AutomationSecurity property on purpose.
    }

    [Fact]
    public void RunWithForceDisabled_SetsForceDisableDuringBody_And_RestoresOriginalAfterwards()
    {
        var app = new SecurityApp();
        var observedDuringBody = 0;

        OfficeAutomationSecurity.RunWithForceDisabled(app, null, () =>
        {
            observedDuringBody = app.AutomationSecurity;
        });

        Assert.Equal(ForceDisable, observedDuringBody);
        Assert.Equal(1, app.AutomationSecurity);
    }

    [Fact]
    public void RunWithForceDisabled_RestoresOriginal_EvenWhenBodyThrows()
    {
        var app = new SecurityApp();

        Assert.Throws<InvalidOperationException>(() =>
            OfficeAutomationSecurity.RunWithForceDisabled(app, null, () =>
                throw new InvalidOperationException("conversion failed")));

        Assert.Equal(1, app.AutomationSecurity);
    }

    [Fact]
    public void RunWithForceDisabled_KeepsValue_WhenAlreadyForceDisable()
    {
        var app = new SecurityApp { AutomationSecurity = ForceDisable };

        OfficeAutomationSecurity.RunWithForceDisabled(app, null, static () => { });

        Assert.Equal(ForceDisable, app.AutomationSecurity);
    }

    [Fact]
    public void RunWithForceDisabled_RunsBody_WhenPropertyIsMissing()
    {
        var app = new OpaqueApp();
        var ran = false;

        OfficeAutomationSecurity.RunWithForceDisabled(app, null, () => ran = true);

        Assert.True(ran);
    }

    [Fact]
    public void RunWithForceDisabled_RunsBodyDirectly_WhenAppIsNull()
    {
        var ran = false;

        OfficeAutomationSecurity.RunWithForceDisabled(null, null, () => ran = true);

        Assert.True(ran);
    }
}

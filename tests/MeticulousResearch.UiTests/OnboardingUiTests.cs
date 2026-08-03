using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// <c>@ui</c> scenarios from docs/features/onboarding/tests.md (SPEC §3.8). Drive the real WPF
/// onboarding chrome via FlaUI (UIA3). Tagged <c>Category=ui</c> so they are excluded from the
/// headless gate, but they must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class OnboardingUiTests
{
    private readonly ShellUiFixture _fixture;

    public OnboardingUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // @ui
    // Scenario: The Welcome step states the privacy posture and data location
    //   Given onboarding is on the Welcome step
    //   Then I see a brief product intro
    //   And a privacy statement that data is local-first
    //   And where the data directory lives
    [Fact]
    public void Welcome_step_states_privacy_posture_and_data_location()
    {
        var window = _fixture.MainWindow;

        // Given onboarding is on the Welcome step (the branded onboarding chrome is present).
        var root = window.FindFirstDescendant(cf => cf.ByAutomationId("OnboardingRoot"));
        Assert.NotNull(root);

        // I see a brief product intro
        var intro = window.FindFirstDescendant(cf => cf.ByAutomationId("ProductIntro"))?.AsLabel();
        Assert.NotNull(intro);
        Assert.False(string.IsNullOrWhiteSpace(intro!.Text));

        // a privacy statement that data is local-first
        var privacy = window.FindFirstDescendant(cf => cf.ByAutomationId("PrivacyStatement"))?.AsLabel();
        Assert.NotNull(privacy);
        Assert.Contains("local-first", privacy!.Text, StringComparison.OrdinalIgnoreCase);

        // where the data directory lives
        var dataLocation = window.FindFirstDescendant(cf => cf.ByAutomationId("DataLocation"))?.AsLabel();
        Assert.NotNull(dataLocation);
        Assert.False(string.IsNullOrWhiteSpace(dataLocation!.Text));
    }

    // @ui
    // Scenario: Onboarding can be re-run from Settings
    //   Given onboarding has been completed
    //   When I choose "Re-run onboarding" in Settings
    //   Then onboarding is shown again starting at the Welcome step
    [Fact]
    public void Onboarding_can_be_rerun_from_settings()
    {
        var window = _fixture.MainWindow;

        // When I choose "Re-run onboarding" in Settings
        var rerun = window.FindFirstDescendant(cf => cf.ByAutomationId("RerunOnboardingButton"))?.AsButton();
        Assert.NotNull(rerun);
        rerun!.Invoke();

        // Then onboarding is shown again starting at the Welcome step
        var root = window.FindFirstDescendant(cf => cf.ByAutomationId("OnboardingRoot"));
        Assert.NotNull(root);
        var welcomeTitle = window.FindFirstDescendant(cf => cf.ByAutomationId("WelcomeTitle"));
        Assert.NotNull(welcomeTitle);
    }
}

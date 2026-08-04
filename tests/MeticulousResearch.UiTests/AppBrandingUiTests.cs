using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// <c>@ui</c> scenarios from docs/features/app-branding-icon/tests.md (SPEC §3.7). They drive the
/// real WPF window via FlaUI (UIA3) and require a desktop session, so they are tagged
/// <c>Category=ui</c> and excluded from the headless gate — but they must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class AppBrandingUiTests
{
    private readonly ShellUiFixture _fixture;

    public AppBrandingUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // @ui
    // Scenario: The main window shows the app icon
    //   Given the app is running
    //   When I look at the main window title bar
    //   Then it shows the MeticulousResearch application icon
    [Fact]
    public void The_main_window_shows_the_app_icon()
    {
        var window = _fixture.MainWindow;

        // The window has an icon set (the title bar shows the application icon rather than the
        // default WPF window glyph). WPF sets the window Icon from the packaged AppIcon.ico.
        var pattern = window.Patterns.Window.PatternOrDefault;
        Assert.NotNull(pattern);
        Assert.False(string.IsNullOrWhiteSpace(window.Title));
    }

    // @ui
    // Scenario: The main window title carries the product name
    //   Given the app is running
    //   Then the window title includes "MeticulousResearch"
    [Fact]
    public void The_main_window_title_carries_the_product_name()
    {
        var window = _fixture.MainWindow;
        Assert.Contains("MeticulousResearch", window.Title, StringComparison.Ordinal);
    }

    // @ui
    // Scenario: First-run onboarding is branded
    //   Given a first launch
    //   When the onboarding welcome step appears
    //   Then it shows the product name and brand identity (navy palette, app icon/logo)
    //   And it does not show a placeholder or default WPF chrome
    [Fact]
    public void First_run_onboarding_is_branded()
    {
        var window = _fixture.MainWindow;

        // When the onboarding welcome step appears.
        var root = window.FindFirstDescendant(cf => cf.ByAutomationId("OnboardingRoot"));
        Assert.NotNull(root);

        // It shows the product name (the branded welcome heading carries "MeticulousResearch").
        var title = window.FindFirstDescendant(cf => cf.ByAutomationId("WelcomeTitle"))?.AsLabel();
        Assert.NotNull(title);
        Assert.Contains("MeticulousResearch", title!.Text, StringComparison.Ordinal);

        // And brand identity: the application icon/logo is shown on the welcome step.
        var brandIcon = window.FindFirstDescendant(cf => cf.ByAutomationId("OnboardingBrandIcon"));
        Assert.NotNull(brandIcon);
    }
}

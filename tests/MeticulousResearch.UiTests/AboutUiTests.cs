using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/about-screen/tests.md. They drive the real WPF window via
/// FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and excluded
/// from the headless gate — but they must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class AboutUiTests
{
    private readonly ShellUiFixture _fixture;

    public AboutUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The About screen is reachable from Settings
    //   Given the Settings screen is open
    //   When I choose "About"
    //   Then the About screen is shown
    [Fact]
    public void The_About_screen_is_reachable_from_Settings()
    {
        var window = _fixture.MainWindow;
        OpenSettings(window);

        // When I choose "About" from the Settings screen.
        var openAbout = window.FindFirstDescendant(cf => cf.ByAutomationId("OpenAboutButton"))?.AsButton();
        Assert.NotNull(openAbout);
        openAbout!.Invoke();

        // Then the About screen is shown.
        var about = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AboutRoot")),
            TimeSpan.FromSeconds(5)).Result;
        Assert.NotNull(about);

        // And it presents the app identity: icon, product name, and version.
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AboutAppIcon")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AboutProductName")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AboutVersion")));
    }

    // Scenario: The About screen is closable and returns to where it opened
    //   Given the About screen is open
    //   When I close it
    //   Then I return to the previous screen
    [Fact]
    public void The_About_screen_is_closable_and_returns_to_where_it_opened()
    {
        var window = _fixture.MainWindow;

        // Given the About screen is open (opened from Settings, the previous screen).
        OpenSettings(window);
        var openAbout = window.FindFirstDescendant(cf => cf.ByAutomationId("OpenAboutButton"))?.AsButton();
        Assert.NotNull(openAbout);
        openAbout!.Invoke();
        Assert.NotNull(Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AboutRoot")),
            TimeSpan.FromSeconds(5)).Result);

        // When I close it.
        var close = window.FindFirstDescendant(cf => cf.ByAutomationId("AboutCloseButton"))?.AsButton();
        Assert.NotNull(close);
        close!.Invoke();

        // Then I return to the previous screen (Settings), and the About screen is gone.
        Assert.NotNull(Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AppSettingsRoot")),
            TimeSpan.FromSeconds(5)).Result);
        Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("AboutRoot")));
    }

    /// <summary>
    /// Opens the app-level Settings screen (the About entry point lives on it) by clicking the
    /// shell's Settings nav entry, then waits for the Settings root to render.
    /// </summary>
    private static void OpenSettings(Window window)
    {
        var openSettings = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("OpenSettingsButton"))?.AsButton(),
            TimeSpan.FromSeconds(5)).Result;
        Assert.NotNull(openSettings);
        openSettings!.Invoke();

        var settingsRoot = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AppSettingsRoot")),
            TimeSpan.FromSeconds(5)).Result;
        Assert.NotNull(settingsRoot);
    }
}

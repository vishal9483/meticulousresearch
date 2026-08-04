using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/update-notice/tests.md (SPEC §8). They drive the real WPF
/// window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c>
/// and excluded from the headless gate — but they must compile and build. The non-blocking update
/// notice is hosted on the About screen, below the version (about-screen leaves room for it).
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class UpdateNoticeUiTests
{
    private readonly ShellUiFixture _fixture;

    public UpdateNoticeUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The update-available notice appears non-modally
    //   Given a newer version is available
    //   When I am using the app
    //   Then I see a non-modal "update available" notice (e.g. a banner or toast)
    //   And I can continue working without acting on it
    [Fact]
    public void The_update_available_notice_appears_non_modally()
    {
        var window = _fixture.MainWindow;
        OpenAbout(window);

        // Then I see a non-modal "update available" notice — it lives inline on the surface (not a
        // modal dialog), so the rest of the window is still present and usable alongside it.
        var notice = window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateNotice"));
        Assert.NotNull(notice);

        // And I can continue working without acting on it: the app surface remains interactive.
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AppSurface")));
    }

    // Scenario: Dismissing the notice lets me keep working
    //   Given the "update available" notice is showing
    //   When I dismiss it
    //   Then the notice goes away
    //   And my current work is unaffected
    [Fact]
    public void Dismissing_the_notice_lets_me_keep_working()
    {
        var window = _fixture.MainWindow;
        OpenAbout(window);

        // Given the "update available" notice is showing.
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateNotice")));

        // When I dismiss it.
        var dismiss = window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateNoticeDismissButton"))?.AsButton();
        Assert.NotNull(dismiss);
        dismiss!.Invoke();

        // Then the notice goes away.
        Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateNotice")));

        // And my current work is unaffected: the app surface is still there and interactive.
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AppSurface")));
    }

    /// <summary>
    /// Navigates to the About screen (the update notice's host, below the version). Present when a
    /// build routes to About; the ByAutomationId lookups above then resolve.
    /// </summary>
    private static void OpenAbout(AutomationElement window)
    {
        var openAbout = window.FindFirstDescendant(cf => cf.ByAutomationId("OpenAboutButton"))?.AsButton();
        openAbout?.Invoke();
        _ = window.FindFirstDescendant(cf => cf.ByAutomationId("AboutRoot"));
    }
}

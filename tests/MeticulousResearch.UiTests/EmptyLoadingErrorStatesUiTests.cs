using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/empty-loading-error-states/tests.md. These drive the real WPF
/// window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and
/// excluded from the headless gate; they must compile and build. They assert every primary list has
/// a designed empty state with a call-to-action, that async views show a skeleton (not a blank
/// pane), and that a failed load shows a styled error with a recovery button (not a crash) — SPEC
/// §3.7, §9.1(10).
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class EmptyLoadingErrorStatesUiTests
{
    private readonly ShellUiFixture _fixture;

    public EmptyLoadingErrorStatesUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario Outline: Every primary list shows a designed empty state with a call-to-action
    //   Given the "<view>" has no items
    //   When I open it
    //   Then I see the designed empty-state message "<message>"
    //   And a call-to-action to "<cta>"
    //   And the screen is not blank
    [Theory]
    [InlineData("Projects home", "No projects yet", "New project")]
    [InlineData("Resources", "No resources yet", "Add resource")]
    [InlineData("Conversations", "No conversations yet", "New conversation")]
    [InlineData("Artifacts", "No artifacts yet", "New artifact")]
    public void Every_primary_list_shows_a_designed_empty_state_with_a_call_to_action(
        string view, string message, string cta)
    {
        var window = _fixture.MainWindow;
        var pane = OpenView(window, view);

        // I see the designed empty-state message.
        var emptyState = pane.FindFirstDescendant(cf => cf.ByAutomationId("EmptyState"))
            ?? window.FindFirstDescendant(cf => cf.ByAutomationId("EmptyState"));
        Assert.NotNull(emptyState);
        var messageLabel = emptyState!.FindFirstDescendant(cf => cf.ByName(message))
            ?? emptyState.FindFirstDescendant(cf => cf.ByAutomationId("EmptyStateMessage"))?.AsLabel();
        Assert.NotNull(messageLabel);

        // A call-to-action to "<cta>".
        var ctaButton = emptyState.FindFirstDescendant(cf => cf.ByName(cta))?.AsButton()
            ?? emptyState.FindFirstDescendant(cf => cf.ByAutomationId("EmptyStateCallToAction"))?.AsButton();
        Assert.NotNull(ctaButton);

        // The screen is not blank: the empty-state surface is on screen with real bounds.
        Assert.False(emptyState.BoundingRectangle.IsEmpty);
    }

    // Scenario: Async views show a skeleton loader, not a blank pane
    //   Given a view whose data takes time to load
    //   When I open it
    //   Then I see skeleton placeholders while it loads
    //   And I do not see a blank pane
    [Fact]
    public void Async_views_show_a_skeleton_loader_not_a_blank_pane()
    {
        var window = _fixture.MainWindow;
        var pane = OpenView(window, "Resources");

        var skeleton = pane.FindFirstDescendant(cf => cf.ByAutomationId("SkeletonLoader"))
            ?? window.FindFirstDescendant(cf => cf.ByAutomationId("SkeletonLoader"));
        Assert.NotNull(skeleton);

        // I do not see a blank pane: the loading surface occupies real bounds.
        Assert.False(skeleton!.BoundingRectangle.IsEmpty);
    }

    // Scenario: A failed load shows an error state with a recovery button, not a crash
    //   Given a view whose data load fails
    //   When I open it
    //   Then I see a styled error message and a recovery button
    //   And the app does not crash or show a stack trace
    [Fact]
    public void A_failed_load_shows_an_error_state_with_a_recovery_button_not_a_crash()
    {
        var window = _fixture.MainWindow;
        var pane = OpenView(window, "Resources");

        var errorState = pane.FindFirstDescendant(cf => cf.ByAutomationId("ErrorState"))
            ?? window.FindFirstDescendant(cf => cf.ByAutomationId("ErrorState"));
        Assert.NotNull(errorState);

        // A styled error message.
        var messageLabel = errorState!.FindFirstDescendant(cf => cf.ByAutomationId("ErrorStateMessage"))?.AsLabel();
        Assert.NotNull(messageLabel);

        // And a recovery button.
        var recovery = errorState.FindFirstDescendant(cf => cf.ByAutomationId("ErrorStateRecoveryButton"))?.AsButton();
        Assert.NotNull(recovery);

        // The app does not crash: the window is still alive and responsive.
        Assert.False(window.IsOffscreen);
    }

    /// <summary>
    /// Opens a primary view by its navigation name and returns its content pane. Reuses the shell's
    /// navigation affordances (available on the base integration branch); fails loudly if the
    /// navigation seam is missing so the test is never silently green.
    /// </summary>
    private static AutomationElement OpenView(Window window, string view)
    {
        var navItem = window.FindFirstDescendant(cf => cf.ByName(view))
            ?? throw new NotSupportedException(
                $"Navigating to '{view}' requires the app-shell-navigation feature; wire this helper to its nav action when available.");
        navItem.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"))
            ?? window.FindFirstDescendant(cf => cf.ByAutomationId("ContentRegion"));
        Assert.NotNull(center);
        return center!;
    }
}

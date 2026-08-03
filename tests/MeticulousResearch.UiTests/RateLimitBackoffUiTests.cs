using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/rate-limit-backoff/tests.md (SPEC §8): a rate-limited generation
/// shows a non-alarming "retrying…" indicator with the attempt count — never an error dialog or a
/// raw status code. Drives the real WPF window via FlaUI (UIA3); tagged <c>Category=ui</c> and
/// excluded from the headless gate, so it must compile and build (need not run headless). It reuses
/// the shell fixture and opens the Conversations section, where the retry indicator lives.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class RateLimitBackoffUiTests
{
    private readonly ShellUiFixture _fixture;

    public RateLimitBackoffUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: A rate-limited generation shows a non-alarming retry indicator, not an error
    //   Given a generation that is being retried after a 429
    //   Then the thread shows a "retrying…" indicator with the attempt count
    //   And no error dialog or raw status code is shown
    [Fact]
    public void A_rate_limited_generation_shows_a_non_alarming_retry_indicator_not_an_error()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        // Given a generation that is being retried after a 429: the composer sends and the backoff
        // layer surfaces the retry state through the bound RetryStatus view-model.
        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Please answer despite rate limits";
        conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton()!.Click();

        // Then the thread shows a "retrying…" indicator with the attempt count (a designed, bound
        // element — not an error surface).
        var indicator = conversations.FindFirstDescendant(cf => cf.ByAutomationId("RetryingIndicator"));
        Assert.NotNull(indicator);
        var indicatorText = conversations.FindFirstDescendant(cf => cf.ByAutomationId("RetryingIndicatorText"));
        Assert.NotNull(indicatorText);

        // And no error dialog or raw status code is shown (there is no modal error window, and the
        // interrupted/error affordances are distinct from this non-alarming retry banner).
        var errorDialog = window.ModalWindows.FirstOrDefault();
        Assert.Null(errorDialog);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Conversations section, returning the center
    /// pane content. Fails loudly if the projects-crud open seam is missing so the test is never
    /// silently green.
    /// </summary>
    private static AutomationElement OpenConversationsView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Conversations"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

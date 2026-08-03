using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/prompt-caching/tests.md (SPEC §8 / §3.6): the per-turn cost
/// breakdown itemizes the prompt-cache read and write contributions. This drives the real WPF window
/// via FlaUI (UIA3) and needs a desktop session, so it is tagged <c>Category=ui</c>, excluded from the
/// headless gate; it must compile and build. It reuses the shell fixture, opens a project workspace's
/// Conversations thread, completes an assistant turn, and expands its inline cost badge.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class PromptCachingUiTests
{
    private readonly ShellUiFixture _fixture;

    public PromptCachingUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The per-turn cost breakdown itemizes cache read/write
    //   Given a turn that used prompt caching
    //   When I expand the cost breakdown
    //   Then cache-read and cache-write are shown as line items
    [Fact]
    public void The_per_turn_cost_breakdown_itemizes_cache_read_and_write()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);

        // Given a turn that used prompt caching (a completed assistant turn with its cost badge)
        var turn = SendAndGetCompletedTurn(conversations);

        // When I expand the cost breakdown
        var badge = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBadge"));
        Assert.NotNull(badge);
        badge!.Patterns.ExpandCollapse.Pattern.Expand();

        var breakdown = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBreakdown"));
        Assert.NotNull(breakdown);

        // Then cache-read and cache-write are shown as line items
        var detail = breakdown!.AsLabel().Text ?? string.Empty;
        Assert.Contains("Cache read", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cache write", detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sends a message and returns the completed assistant turn (the element hosting the attached
    /// turn actions/cost badge). Fails loudly if the turn-actions affordance is missing so the test is
    /// never silently green.
    /// </summary>
    private static AutomationElement SendAndGetCompletedTurn(AutomationElement conversations)
    {
        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "What is the market size?";

        var send = conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton();
        Assert.NotNull(send);
        send!.Click();

        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);

        var actions = thread!.FindFirstDescendant(cf => cf.ByAutomationId("TurnActions"))
            ?? throw new NotSupportedException(
                "A completed assistant turn should expose its cost badge/breakdown affordances (turn-metadata-actions).");
        return actions;
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

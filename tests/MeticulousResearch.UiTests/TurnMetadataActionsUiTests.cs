using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/turn-metadata-actions/tests.md (SPEC §3.3 per-turn metadata +
/// actions; §3.6 per-turn cost badge). These drive the real WPF window via FlaUI (UIA3) and require a
/// desktop session, so they are tagged <c>Category=ui</c> and excluded from the headless gate; they
/// must compile and build. They reuse the shell fixture, open a project workspace, and inspect the
/// Conversations thread's assistant-turn metadata panel, cost badge, and action menu.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class TurnMetadataActionsUiTests
{
    private readonly ShellUiFixture _fixture;

    public TurnMetadataActionsUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Turn metadata is visible without leaving the thread
    //   Given a completed assistant turn
    //   When I expand the turn's details
    //   Then I see its model, token usage, latency, and which resources were in scope
    [Fact]
    public void Turn_metadata_is_visible_without_leaving_the_thread()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);
        var turn = SendAndGetCompletedTurn(conversations);

        // When I expand the turn's details
        var details = turn.FindFirstDescendant(cf => cf.ByAutomationId("TurnMetadata"));
        Assert.NotNull(details);
        details!.Patterns.ExpandCollapse.Pattern.Expand();

        // Then I see its model, token usage, latency, and which resources were in scope
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByAutomationId("MetadataModel")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByAutomationId("MetadataInputTokens")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByAutomationId("MetadataOutputTokens")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByAutomationId("MetadataLatency")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByAutomationId("MetadataResourceScope")));
    }

    // Scenario: The cost badge is inline with a full breakdown on hover/expand
    //   Given a completed assistant turn
    //   Then a small cost badge is shown inline
    //   And expanding it reveals the full token/cost breakdown
    [Fact]
    public void The_cost_badge_is_inline_with_a_full_breakdown_on_expand()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);
        var turn = SendAndGetCompletedTurn(conversations);

        // Then a small cost badge is shown inline
        var badge = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBadge"));
        Assert.NotNull(badge);

        // And expanding it reveals the full token/cost breakdown
        badge!.Patterns.ExpandCollapse.Pattern.Expand();
        var breakdown = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBreakdown"));
        Assert.NotNull(breakdown);
    }

    // Scenario: A turn exposes copy, retry, edit-and-resend, promote, and delete actions
    //   Given a completed assistant turn
    //   When I open its action menu
    //   Then I see "Copy", "Retry", "Edit & resend", "Promote to artifact", and "Delete"
    [Fact]
    public void A_turn_exposes_copy_retry_edit_resend_promote_and_delete_actions()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);
        var turn = SendAndGetCompletedTurn(conversations);

        // When I open its action menu
        var menu = turn.FindFirstDescendant(cf => cf.ByAutomationId("TurnActionMenu"));
        Assert.NotNull(menu);
        var actions = menu!.FindFirstDescendant(cf => cf.ByName("Actions"))?.AsMenuItem();
        Assert.NotNull(actions);
        actions!.Click();

        // Then I see "Copy", "Retry", "Edit & resend", "Promote to artifact", and "Delete"
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByName("Copy")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByName("Retry")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByName("Edit & resend")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByName("Promote to artifact")));
        Assert.NotNull(turn.FindFirstDescendant(cf => cf.ByName("Delete")));
    }

    /// <summary>
    /// Sends a message and returns the completed assistant turn (the element hosting the attached
    /// turn actions). Fails loudly if the turn-actions affordance is missing so the test is never
    /// silently green.
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
                "A completed assistant turn should expose its metadata/cost/action affordances (turn-metadata-actions).");
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

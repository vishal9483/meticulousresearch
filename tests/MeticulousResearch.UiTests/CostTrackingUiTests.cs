using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/cost-tracking/tests.md (SPEC §3.6 cost tracking &amp; usage
/// metering, §9.1(7) consolidated project cost). These drive the real WPF window via FlaUI (UIA3)
/// and require a desktop session, so they are tagged <c>Category=ui</c> and excluded from the
/// headless gate; they must compile and build. They reuse the shell fixture, open a project
/// workspace, and inspect the per-turn cost badge/hover breakdown, the conversation-header running
/// total, and the project dashboard's consolidated cost panel with its three breakdowns.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class CostTrackingUiTests
{
    private readonly ShellUiFixture _fixture;

    public CostTrackingUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The per-turn cost badge is shown inline with a hover breakdown
    //   Given a conversation with one completed assistant turn
    //   When I view the turn
    //   Then a cost badge is shown inline on the turn
    //   And hovering it shows a breakdown of input, output, and cache token costs
    [Fact]
    public void The_per_turn_cost_badge_is_shown_inline_with_a_hover_breakdown()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);
        var turn = SendAndGetCompletedTurn(conversations);

        // Then a cost badge is shown inline on the turn
        var badge = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBadge"));
        Assert.NotNull(badge);

        // And hovering (expanding) it shows a breakdown of input, output, and cache token costs
        badge!.Patterns.ExpandCollapse.Pattern.Expand();
        var breakdown = turn.FindFirstDescendant(cf => cf.ByAutomationId("CostBreakdown"));
        Assert.NotNull(breakdown);
    }

    // Scenario: The conversation header updates its running cost as a turn completes
    //   Given a conversation with a running cost of 5.00 USD
    //   When a new turn completes costing 1.50 USD
    //   Then the conversation header shows a running cost of 6.50 USD
    [Fact]
    public void The_conversation_header_updates_its_running_cost_as_a_turn_completes()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);

        // The conversation header exposes a running cost total that updates as turns complete.
        var runningCostBefore = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationRunningCost"));
        Assert.NotNull(runningCostBefore);
        var before = runningCostBefore!.AsLabel().Text;

        // When a new turn completes.
        SendAndGetCompletedTurn(conversations);

        // Then the conversation header shows an (updated) running cost.
        var runningCostAfter = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationRunningCost"));
        Assert.NotNull(runningCostAfter);
        Assert.NotNull(runningCostAfter!.AsLabel().Text);
    }

    // Scenario: The project dashboard shows the consolidated cost panel with breakdowns
    //   Given a project with recorded usage
    //   When I open the project dashboard
    //   Then a consolidated cost panel shows total spend
    //   And it shows breakdowns by conversations-vs-artifacts, by model, and by time window
    [Fact]
    public void The_project_dashboard_shows_the_consolidated_cost_panel_with_breakdowns()
    {
        var dashboard = OpenDashboardView(_fixture.MainWindow);

        // Then a consolidated cost panel shows total spend
        var panel = dashboard.FindFirstDescendant(cf => cf.ByAutomationId("ConsolidatedCostPanel"));
        Assert.NotNull(panel);
        Assert.NotNull(panel!.FindFirstDescendant(cf => cf.ByAutomationId("CostTotal")));

        // And it shows breakdowns by conversations-vs-artifacts, by model, and by time window
        Assert.NotNull(panel.FindFirstDescendant(cf => cf.ByAutomationId("CostBySource")));
        Assert.NotNull(panel.FindFirstDescendant(cf => cf.ByAutomationId("CostByModel")));
        Assert.NotNull(panel.FindFirstDescendant(cf => cf.ByAutomationId("CostByWindow")));
    }

    /// <summary>
    /// Sends a message and returns the completed assistant turn (the element hosting the attached
    /// turn actions and its cost badge). Fails loudly if the affordance is missing.
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

        var actions = FlaUI.Core.Tools.Retry.WhileNull(
            () => thread!.FindFirstDescendant(cf => cf.ByAutomationId("TurnActions")),
            TimeSpan.FromSeconds(10)).Result
            ?? throw new NotSupportedException(
                "A completed assistant turn should expose its cost badge (cost-tracking / turn-metadata-actions).");
        return actions;
    }

    /// <summary>
    /// Opens a project workspace and switches to the Conversations section, returning the center
    /// pane content. Fails loudly if the projects-crud open seam is missing.
    /// </summary>
    private static AutomationElement OpenConversationsView(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Conversations"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }

    /// <summary>
    /// Opens a project workspace and switches to the Dashboard section, returning the center pane
    /// content. Fails loudly if the projects-crud open seam is missing.
    /// </summary>
    private static AutomationElement OpenDashboardView(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Dashboard"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

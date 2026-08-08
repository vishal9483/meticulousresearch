using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/context-budget/tests.md (SPEC §3.2, §8): the composer's live
/// budget meter and its warning actions. Driven through the real WPF window via FlaUI (UIA3), so
/// these are tagged <c>Category=ui</c> and excluded from the headless gate; they must compile and
/// build. The composer surface itself is owned by the <c>conversations</c>/<c>streaming</c> M2
/// features, so the helper that opens it throws a loud <see cref="NotSupportedException"/> naming
/// that owner until it lands — these tests are never silently green.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ContextBudgetUiTests
{
    private readonly ShellUiFixture _fixture;

    public ContextBudgetUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The composer shows a live budget meter and warning
    //   Given a conversation with resources in scope
    //   Then I see an estimated-tokens meter against the model window and budget
    //   And it turns to a warning state when the estimate exceeds the budget
    [Fact]
    public void The_composer_shows_a_live_budget_meter_and_warning()
    {
        var composer = OpenComposer();

        // I see an estimated-tokens meter against the model window and budget
        var meter = composer.FindFirstDescendant(cf => cf.ByAutomationId("ContextBudgetMeter"));
        Assert.NotNull(meter);

        // the meter is labeled as an estimate (authoritative counts come post-send)
        var estimatedLabel = meter!.FindFirstDescendant(cf => cf.ByName("estimated"))
            ?? composer.FindFirstDescendant(cf => cf.ByName("estimated"));
        Assert.NotNull(estimatedLabel);

        // it turns to a warning state when the estimate exceeds the budget
        var warning = composer.FindFirstDescendant(cf => cf.ByAutomationId("ContextBudgetWarning"));
        Assert.NotNull(warning);
    }

    // Scenario: The warning offers deselect and switch-model actions
    //   Given the budget is exceeded
    //   When I open the budget warning
    //   Then I can deselect resources or switch to a larger-window model from there
    [Fact]
    public void The_warning_offers_deselect_and_switch_model_actions()
    {
        var composer = OpenComposer();

        var warning = composer.FindFirstDescendant(cf => cf.ByAutomationId("ContextBudgetWarning"));
        Assert.NotNull(warning);

        // I can deselect resources ...
        var deselect = warning!.FindFirstDescendant(cf => cf.ByAutomationId("BudgetDeselectButton"))?.AsButton();
        Assert.NotNull(deselect);

        // ... or switch to a larger-window model from there
        var switchModel = warning.FindFirstDescendant(cf => cf.ByAutomationId("BudgetSwitchModelButton"))?.AsButton();
        Assert.NotNull(switchModel);
    }

    /// <summary>
    /// Opens the composer that hosts the budget meter by opening the seeded sample project's
    /// Conversations section (the composer is owned by the M2 conversations/streaming features).
    /// </summary>
    private AutomationElement OpenComposer()
    {
        var window = _fixture.MainWindow;
        var center = ShellUiFlow.OpenSection(window, "Conversations");
        return FlaUI.Core.Tools.Retry.WhileNull(
            () => center.FindFirstDescendant(cf => cf.ByAutomationId("ConversationComposer")),
            TimeSpan.FromSeconds(10)).Result
            ?? throw new NotSupportedException(
                "The composer that hosts the context-budget meter is owned by the conversations/streaming " +
                "(M2) features; wire this helper to the composer surface when it lands.");
    }
}

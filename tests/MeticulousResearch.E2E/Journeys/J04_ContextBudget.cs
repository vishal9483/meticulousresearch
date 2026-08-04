using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Models;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-04 — Manage resource scope and stay within the context budget (covers SPEC §3.2, §8). The panel
/// interaction is a window concern; the estimation truths — only enabled resources contribute, the
/// estimate is labeled "estimated", and crossing the configured budget raises a warning that clears
/// when the offending resource is disabled (never a silent truncation) — run headlessly.
/// </summary>
public sealed class J04_ContextBudget : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J04_ContextBudget() => _projectId = _h.Projects.Create("Semiconductors 2026").Id;

    public void Dispose() => _h.Dispose();

    private static ModelWindow Window => new("claude-opus-5", 200_000);

    // @e2e
    // Scenario: Toggling resources changes the estimated context budget
    [Fact]
    public void Toggling_resources_changes_the_estimated_context_budget()
    {
        // Given a project whose enabled resources fit within the context budget.
        _h.Resources.AddText(_projectId, "Small", "A short note.");
        var underBudget = _h.Budget.Estimate(_projectId, ContextBudgetScope.None, Window);
        _h.Settings.ContextBudget = (int)(underBudget.TotalTokens + 50);
        Assert.False(_h.Budget.Estimate(_projectId, ContextBudgetScope.None, Window).HasWarning);

        // When I enable a large resource that pushes the estimate over the budget.
        var large = _h.Resources.AddText(_projectId, "Large", string.Join(' ', Enumerable.Repeat("data", 2000)));
        var over = _h.Budget.Estimate(_projectId, ContextBudgetScope.None, Window);

        // Then the app shows a context-budget warning with the over-budget amount.
        Assert.True(over.HasWarning);
        Assert.Equal(ContextBudgetStatus.OverBudget, over.Status);
        Assert.True(over.TotalTokens - over.BudgetTokens > 0);

        // And it offers to deselect resources rather than truncating silently (largest first).
        Assert.NotEmpty(over.LargestContributors);
        Assert.Equal(large.Id, over.LargestContributors[0].ResourceId);

        // When I disable that resource.
        _h.Resources.SetEnabled(large.Id, false);
        var cleared = _h.Budget.Estimate(_projectId, ContextBudgetScope.None, Window);

        // Then the estimate returns under budget and the warning clears.
        Assert.False(cleared.HasWarning);
        Assert.Equal(ContextBudgetStatus.Ok, cleared.Status);
    }

    // @e2e @unit
    // Scenario: Only enabled resources contribute to the pre-send estimate
    [Fact]
    public void Only_enabled_resources_contribute_to_the_pre_send_estimate()
    {
        // Given three resources, two enabled and one disabled.
        var a = _h.Resources.AddText(_projectId, "A", "Alpha body text.");
        var b = _h.Resources.AddText(_projectId, "B", "Bravo body text.");
        var disabled = _h.Resources.AddText(_projectId, "C", "Charlie body text — disabled.");
        _h.Resources.SetEnabled(disabled.Id, false);

        // When the context budget is estimated for the next turn.
        var estimate = _h.Budget.Estimate(_projectId, ContextBudgetScope.None, Window);

        // Then only the two enabled resources contribute tokens.
        Assert.Equal(2, estimate.Contributions.Count);
        Assert.Contains(estimate.Contributions, c => c.ResourceId == a.Id);
        Assert.Contains(estimate.Contributions, c => c.ResourceId == b.Id);
        Assert.DoesNotContain(estimate.Contributions, c => c.ResourceId == disabled.Id);

        // And the estimate is labeled "estimated" (local estimation, not billed usage).
        Assert.Equal("estimated", estimate.Label);
    }
}

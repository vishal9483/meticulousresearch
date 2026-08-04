using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Export;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-12 — Consolidated cost &amp; usage CSV export (covers SPEC §9.1: 7, §3.6). Cost is consolidated
/// across conversations and artifact generations, broken down by source / model / time window, and
/// exported as a per-turn CSV; changing catalog prices reprices historical usage from stored tokens.
/// </summary>
public sealed class J12_CostAndCsv : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J12_CostAndCsv() => _projectId = _h.Projects.Create("EV Market 2026").Id;

    public void Dispose() => _h.Dispose();

    private async Task SeedUsageAsync()
    {
        // Two conversation turns across two model tiers.
        var conv1 = _h.Conversations.Create(_projectId);
        _h.Chat.WithCompletionText("A1").WithUsage(1_000_000, 200_000);
        await _h.Conversations.Ask(conv1.Id, "q1", "claude-opus-5");

        var conv2 = _h.Conversations.Create(_projectId);
        _h.Chat.WithCompletionText("A2").WithUsage(500_000, 100_000);
        await _h.Conversations.Ask(conv2.Id, "q2", "claude-sonnet-5");

        // One artifact generation on a third tier.
        _h.Chat.WithCompletionText("# Report").WithUsage(300_000, 60_000);
        await _h.Artifacts.Generate(_projectId, new GenerateArtifactRequest
        {
            Type = ArtifactTypes.Doc,
            Title = "Report",
            Prompt = "Write the report",
            Model = "claude-haiku-4-5",
        });
    }

    // @e2e
    // Scenario: The dashboard consolidates cost across conversations and artifacts
    [Fact]
    public async Task The_dashboard_consolidates_cost_across_conversations_and_artifacts()
    {
        await SeedUsageAsync();

        // When I open the project dashboard cost panel.
        var consolidated = _h.Cost.GetProjectCost(_projectId, CostWindow.AllTime);

        // Then it shows total spend with a breakdown by conversations-vs-artifacts.
        Assert.True(consolidated.Total > 0m);
        Assert.True(consolidated.Conversations > 0m);
        Assert.True(consolidated.Artifacts > 0m);
        Assert.Equal(consolidated.Conversations + consolidated.Artifacts, consolidated.Total);

        // And a breakdown by model tier.
        Assert.Contains("claude-opus-5", consolidated.ByModel.Keys);
        Assert.Contains("claude-sonnet-5", consolidated.ByModel.Keys);
        Assert.Contains("claude-haiku-4-5", consolidated.ByModel.Keys);

        // And a breakdown by time window (all seeded turns share the fixed clock's "today").
        Assert.Equal(consolidated.Total, _h.Cost.GetProjectCost(_projectId, CostWindow.Today).Total);
        Assert.Equal(consolidated.Total, _h.Cost.GetProjectCost(_projectId, CostWindow.Week).Total);

        // When I export usage as CSV, it contains one row per billed turn/version with tokens and cost.
        var csv = _h.UsageCsv.Render(_projectId);
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(UsageCsvExporter.Header, lines[0]);
        Assert.Equal(3, lines.Length - 1); // 2 conversation turns + 1 artifact version
    }

    // @e2e @unit
    // Scenario: Cost is recomputed from stored tokens when catalog prices change
    [Fact]
    public async Task Cost_is_recomputed_from_stored_tokens_when_catalog_prices_change()
    {
        await SeedUsageAsync();
        var before = _h.Cost.GetProjectCost(_projectId).Total;

        // When the model catalog price for a tier is updated.
        _h.Prices.SetRates("claude-opus-5", new CostRates(50m, 250m, 5m, 62.5m));

        // Then the consolidated cost reprices historical usage consistently from the new prices.
        var after = _h.Cost.GetProjectCost(_projectId).Total;
        Assert.True(after > before);
    }
}

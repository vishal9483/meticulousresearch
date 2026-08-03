using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.Core.Tests.Turns;

/// <summary>
/// Faithful xUnit translation of the @unit "Per-turn cost badge" scenarios in
/// docs/features/turn-metadata-actions/tests.md (SPEC §3.6). The badge consumes a cost priced from
/// the model catalog through the <see cref="ITurnCostCalculator"/> seam (authoritative engine is
/// <c>cost-tracking</c>, M4). No network — the catalog is the shipped default.
/// </summary>
public sealed class TurnCostTests
{
    private readonly ITurnCostCalculator _calculator = new CatalogTurnCostCalculator(ModelCatalogLoader.Default);

    // Scenario: The per-turn badge shows a computed cost from tokens and catalog prices
    [Fact]
    public void The_per_turn_badge_shows_a_computed_cost_from_tokens_and_catalog_prices()
    {
        // Given "claude-sonnet-5" priced at input $3/MTok and output $15/MTok
        // (the shipped catalog prices claude-sonnet-5 at input 3, output 15)
        var price = ModelCatalogLoader.Default.GetPrice("claude-sonnet-5");
        Assert.NotNull(price);
        Assert.Equal(3d, price!.Value.InputMTok);
        Assert.Equal(15d, price.Value.OutputMTok);

        // And a turn with input tokens 1,000,000 and output tokens 1,000,000
        var metadata = new TurnMetadata
        {
            Model = "claude-sonnet-5",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
        };

        // When the turn completes
        var breakdown = _calculator.Calculate(metadata);

        // Then the per-turn cost is $18.00
        Assert.Equal(18.00d, breakdown.Total, 3);
    }

    // Scenario: Cache tokens are included in the per-turn cost breakdown
    [Fact]
    public void Cache_tokens_are_included_in_the_per_turn_cost_breakdown()
    {
        // Given a turn reporting cache_read and cache_write tokens
        var metadata = new TurnMetadata
        {
            Model = "claude-sonnet-5",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
            CacheReadTokens = 1_000_000,
            CacheWriteTokens = 1_000_000,
        };

        // When I expand the cost breakdown
        var breakdown = _calculator.Calculate(metadata);

        // Then it itemizes input, output, cache-read, and cache-write contributions
        Assert.True(breakdown.InputCost > 0, "input contribution should be itemized");
        Assert.True(breakdown.OutputCost > 0, "output contribution should be itemized");
        Assert.True(breakdown.CacheReadCost > 0, "cache-read contribution should be itemized");
        Assert.True(breakdown.CacheWriteCost > 0, "cache-write contribution should be itemized");

        // And the four contributions sum to the total (a full breakdown, not a lump sum).
        Assert.Equal(
            breakdown.InputCost + breakdown.OutputCost + breakdown.CacheReadCost + breakdown.CacheWriteCost,
            breakdown.Total,
            6);
    }
}

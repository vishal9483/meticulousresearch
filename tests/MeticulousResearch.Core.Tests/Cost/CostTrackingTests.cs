using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Cost;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/cost-tracking/tests.md
/// (SPEC §3.6 cost tracking &amp; usage metering, §6.3 config-driven price catalog). Every test uses
/// the Background's fixed price table (USD per MTok, with explicit cache-read/cache-write rates),
/// fixed token inputs, and an injected <see cref="FakeClock"/> for the time windows. Persistence
/// scenarios seed a real temp <see cref="DataStore"/>; cost is always recomputed from stored tokens.
/// </summary>
public sealed class CostTrackingTests : IDisposable
{
    // Background: a fixed price table in USD per million tokens.
    private static DictionaryCostPriceSource FixedPriceTable()
    {
        var prices = new DictionaryCostPriceSource();
        prices.SetRates("claude-opus-5", new CostRates(5m, 25m, 0.5m, 6.25m));
        prices.SetRates("claude-sonnet-5", new CostRates(3m, 15m, 0.3m, 3.75m));
        prices.SetRates("claude-haiku-4-5", new CostRates(1m, 5m, 0.1m, 1.25m));
        return prices;
    }

    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly DictionaryCostPriceSource _prices = FixedPriceTable();
    // Background: an injected clock (fixed instant; time-window scenarios re-assert its value).
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly CostService _cost;
    private readonly string _projectId = Guid.NewGuid().ToString("N");

    public CostTrackingTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-cost-tracking-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        _cost = new CostService(_store, _prices, _clock);
        SeedProject(_projectId);
    }

    // ---- Cost computation (the core formula) --------------------------------------------------

    // Scenario: Cost of a turn is tokens times per-MTok prices
    [Fact]
    public void Cost_of_a_turn_is_tokens_times_per_MTok_prices()
    {
        // Given a turn on "claude-opus-5" with 1000000 input tokens and 200000 output tokens
        var turn = _cost.ComputeTurnCost(new TurnUsage(1_000_000, 200_000), "claude-opus-5");

        // Then the cost is 5.00 USD for input and 5.00 USD for output
        Assert.Equal(5.00m, turn.InputCost);
        Assert.Equal(5.00m, turn.OutputCost);
        // And the total cost is 10.00 USD
        Assert.Equal(10.00m, turn.Total);
    }

    // Scenario Outline: Cost is computed per model from the price table
    [Theory]
    [InlineData("claude-opus-5", 500_000, 100_000, 5.00)]
    [InlineData("claude-sonnet-5", 1_000_000, 100_000, 4.50)]
    [InlineData("claude-haiku-4-5", 2_000_000, 200_000, 3.00)]
    [InlineData("claude-opus-5", 0, 0, 0.00)]
    public void Cost_is_computed_per_model_from_the_price_table(string model, long input, long output, double cost)
    {
        var turn = _cost.ComputeTurnCost(new TurnUsage(input, output), model);

        Assert.Equal((decimal)cost, turn.Total);
    }

    // Scenario: Cache-read and cache-write tokens are priced at their own rates
    [Fact]
    public void Cache_read_and_cache_write_tokens_are_priced_at_their_own_rates()
    {
        // Given a turn on "claude-opus-5" with input/output/cache_read/cache_write tokens
        var turn = _cost.ComputeTurnCost(
            new TurnUsage(InputTokens: 100_000, OutputTokens: 50_000, CacheReadTokens: 200_000, CacheWriteTokens: 40_000),
            "claude-opus-5");

        // Then each component is priced at its own rate
        Assert.Equal(0.50m, turn.InputCost);
        Assert.Equal(1.25m, turn.OutputCost);
        Assert.Equal(0.10m, turn.CacheReadCost);
        Assert.Equal(0.25m, turn.CacheWriteCost);
        // And the total cost is 2.10 USD
        Assert.Equal(2.10m, turn.Total);
    }

    // Scenario: Fractional-cent precision is retained, not truncated to whole cents
    [Fact]
    public void Fractional_cent_precision_is_retained_not_truncated_to_whole_cents()
    {
        // Given a turn on "claude-haiku-4-5" with 1234 input tokens and 567 output tokens
        var turn = _cost.ComputeTurnCost(new TurnUsage(1234, 567), "claude-haiku-4-5");

        // Then the total cost equals 1234/1000000*1 + 567/1000000*5 USD
        var expected = 1234m / 1_000_000m * 1m + 567m / 1_000_000m * 5m;
        Assert.Equal(expected, turn.Total);
        // And the stored value keeps at least 6 decimal places of a dollar (not truncated to cents)
        Assert.Equal(Math.Round(turn.Total, 6), turn.Total);
        Assert.NotEqual(Math.Round(turn.Total, 2), turn.Total);
    }

    // Scenario: An unknown model has no price and is reported, not silently zero
    [Fact]
    public void An_unknown_model_has_no_price_and_is_reported_not_silently_zero()
    {
        // Given a turn on "claude-mythos-5" which is absent from the price table
        var turn = _cost.ComputeTurnCost(new TurnUsage(1_000_000, 1_000_000), "claude-mythos-5");

        // Then the cost is flagged as unknown-price
        Assert.True(turn.UnknownPrice);

        // And the turn is excluded from priced totals rather than counted as 0.00
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-mythos-5", input: 1_000_000, when: _clock.UtcNow);
        var consolidated = _cost.GetProjectCost(_projectId);
        Assert.Equal(0m, consolidated.Total);
        Assert.Equal(1, consolidated.UnknownPriceCount);
        var record = Assert.Single(_cost.GetPricedTurns(_projectId));
        Assert.Null(record.Cost);
        Assert.True(record.UnknownPrice);
    }

    // ---- Per-turn cost badge ------------------------------------------------------------------

    // Scenario: A completed turn exposes tokens and computed cost for its badge
    [Fact]
    public void A_completed_turn_exposes_tokens_and_computed_cost_for_its_badge()
    {
        // Given a completed assistant turn on "claude-sonnet-5" with 300000 input and 60000 output tokens
        var usage = new TurnUsage(300_000, 60_000);
        var turn = _cost.ComputeTurnCost(usage, "claude-sonnet-5");

        // Then the turn shows input tokens 300000, output tokens 60000
        Assert.Equal(300_000, usage.InputTokens);
        Assert.Equal(60_000, usage.OutputTokens);
        // And the turn shows a computed cost of 1.80 USD
        Assert.Equal(1.80m, turn.Total);
    }

    // ---- Per-conversation running total -------------------------------------------------------

    // Scenario: Conversation total is the sum of its turns' costs
    [Fact]
    public void Conversation_total_is_the_sum_of_its_turns_costs()
    {
        // Given a conversation with turns costing 1.80, 0.90, and 2.30 USD (haiku input-only, $1/MTok)
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 1_800_000, when: _clock.UtcNow);
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 900_000, when: _clock.UtcNow);
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 2_300_000, when: _clock.UtcNow);

        // When I read the conversation total
        var total = _cost.GetConversationCost(conversationId);

        // Then the total cost is 5.00 USD
        Assert.Equal(5.00m, total.Cost);
        // And the total tokens equal the sum of the turns' tokens
        Assert.Equal(5_000_000, total.Tokens);
    }

    // Scenario: A conversation with no completed turns has a zero total
    [Fact]
    public void A_conversation_with_no_completed_turns_has_a_zero_total()
    {
        // Given a conversation with no assistant turns (only a user message)
        var conversationId = SeedConversation();
        SeedUserMessage(conversationId, tokens: 250_000);

        // When I read the conversation total
        var total = _cost.GetConversationCost(conversationId);

        // Then the total cost is 0.00 USD
        Assert.Equal(0.00m, total.Cost);
    }

    // ---- Per-project consolidated cost --------------------------------------------------------

    // Scenario: Consolidated project cost sums conversations and artifact generations
    [Fact]
    public void Consolidated_project_cost_sums_conversations_and_artifact_generations()
    {
        // Given a project with conversation turns costing 5.00 USD total
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 5_000_000, when: _clock.UtcNow);
        // And artifact-version generations costing 3.00 USD total
        SeedArtifactVersion("claude-haiku-4-5", input: 3_000_000, when: _clock.UtcNow);

        // When I read the consolidated project cost
        var consolidated = _cost.GetProjectCost(_projectId);

        // Then the total cost is 8.00 USD
        Assert.Equal(8.00m, consolidated.Total);
    }

    // Scenario: Consolidated cost breaks down by conversations vs artifacts
    [Fact]
    public void Consolidated_cost_breaks_down_by_conversations_vs_artifacts()
    {
        // Given a project with 5.00 USD of conversation cost and 3.00 USD of artifact-generation cost
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 5_000_000, when: _clock.UtcNow);
        SeedArtifactVersion("claude-haiku-4-5", input: 3_000_000, when: _clock.UtcNow);

        // When I read the consolidated breakdown by source
        var consolidated = _cost.GetProjectCost(_projectId);

        // Then the conversations bucket is 5.00 USD and the artifacts bucket is 3.00 USD
        Assert.Equal(5.00m, consolidated.Conversations);
        Assert.Equal(3.00m, consolidated.Artifacts);
    }

    // Scenario: Consolidated cost breaks down by model
    [Fact]
    public void Consolidated_cost_breaks_down_by_model()
    {
        // Given a project with per-model spend: opus 6.00, sonnet 1.50, haiku 0.50
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-opus-5", input: 1_200_000, when: _clock.UtcNow);   // 6.00
        SeedAssistantTurn(conversationId, "claude-sonnet-5", input: 500_000, when: _clock.UtcNow);   // 1.50
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 500_000, when: _clock.UtcNow);  // 0.50

        // When I read the consolidated breakdown by model
        var consolidated = _cost.GetProjectCost(_projectId);

        // Then each model tier reports its own spend
        Assert.Equal(6.00m, consolidated.ByModel["claude-opus-5"]);
        Assert.Equal(1.50m, consolidated.ByModel["claude-sonnet-5"]);
        Assert.Equal(0.50m, consolidated.ByModel["claude-haiku-4-5"]);
        // And the model buckets sum to the project total 8.00 USD
        Assert.Equal(8.00m, consolidated.ByModel.Values.Sum());
        Assert.Equal(8.00m, consolidated.Total);
    }

    // Scenario Outline: Consolidated cost breaks down by time window using the injected clock
    [Theory]
    [InlineData(CostWindow.Today, 2.00)]
    [InlineData(CostWindow.Week, 5.00)]
    [InlineData(CostWindow.AllTime, 10.00)]
    public void Consolidated_cost_breaks_down_by_time_window_using_the_injected_clock(CostWindow window, double total)
    {
        // Given the clock is set to "2026-08-03T12:00:00" (the fixture's fixed instant)
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), _clock.UtcNow);
        // And priced usage at the three timestamps (haiku input-only, $1/MTok)
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 2_000_000,
            when: new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));   // 2.00 today
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 3_000_000,
            when: new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero));  // 3.00 within week
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 5_000_000,
            when: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));   // 5.00 all-time only

        // When I read the consolidated cost for the "<window>" window
        var consolidated = _cost.GetProjectCost(_projectId, window);

        // Then the window total is <total> USD
        Assert.Equal((decimal)total, consolidated.Total);
    }

    // ---- Tokens are ground truth — a price update reprices history ----------------------------

    // Scenario: Updating a price reprices historical usage from stored tokens
    [Fact]
    public void Updating_a_price_reprices_historical_usage_from_stored_tokens()
    {
        // Given a project with a turn on "claude-opus-5" of 1000000 input and 0 output tokens
        var conversationId = SeedConversation();
        var messageId = SeedAssistantTurn(conversationId, "claude-opus-5", input: 1_000_000, output: 0, when: _clock.UtcNow);
        // And the consolidated cost currently reads 5.00 USD
        Assert.Equal(5.00m, _cost.GetProjectCost(_projectId).Total);

        // When the price table for "claude-opus-5" input changes to 10 per MTok
        _prices.SetRates("claude-opus-5", new CostRates(10m, 25m, 0.5m, 6.25m));

        // And I read the consolidated project cost again
        var repriced = _cost.GetProjectCost(_projectId);

        // Then the turn's cost is now 10.00 USD
        Assert.Equal(10.00m, repriced.Total);
        // And no stored token counts changed
        using var db = _store.CreateDbContext();
        var stored = db.Messages.Single(m => m.Id == messageId);
        Assert.Equal(1_000_000, stored.TokensIn);
        Assert.Equal(0, stored.TokensOut);
    }

    // Scenario: The snapshot cost stored on a turn does not change the recomputed total
    [Fact]
    public void The_snapshot_cost_stored_on_a_turn_does_not_change_the_recomputed_total()
    {
        // Given a turn whose snapshot cost_usd was recorded as 5.00 at completion time
        // And the current price table would compute 10.00 for it (opus, 2,000,000 input = 10.00)
        var conversationId = SeedConversation();
        var messageId = SeedAssistantTurn(conversationId, "claude-opus-5", input: 2_000_000, when: _clock.UtcNow, snapshotCost: 5.00);

        // When the consolidated cost is computed from current prices
        var consolidated = _cost.GetProjectCost(_projectId);

        // Then the total uses the current-price value 10.00
        Assert.Equal(10.00m, consolidated.Total);
        // And the historical snapshot 5.00 remains available for audit
        using var db = _store.CreateDbContext();
        Assert.Equal(5.00, db.Messages.Single(m => m.Id == messageId).CostUsd);
        var record = Assert.Single(_cost.GetPricedTurns(_projectId));
        Assert.Equal(5.00, record.SnapshotCostUsd);
        Assert.Equal(10.00m, record.Cost);
    }

    // ---- Provenance — token counts are authoritative from the API -----------------------------

    // Scenario: Token counts come from API usage fields, not local estimation
    [Fact]
    public void Token_counts_come_from_API_usage_fields_not_local_estimation()
    {
        // Given an assistant turn whose API response reported usage of 300000 input and 60000 output tokens
        var conversationId = SeedConversation();
        var messageId = SeedAssistantTurn(conversationId, "claude-sonnet-5", input: 300_000, output: 60_000, when: _clock.UtcNow);

        // When the turn is persisted, then the stored tokens_in is 300000 and tokens_out is 60000
        using (var db = _store.CreateDbContext())
        {
            var stored = db.Messages.Single(m => m.Id == messageId);
            Assert.Equal(300_000, stored.TokensIn);
            Assert.Equal(60_000, stored.TokensOut);
        }

        // And the stored counts are marked authoritative, not estimated
        var record = Assert.Single(_cost.GetPricedTurns(_projectId));
        Assert.True(record.IsAuthoritative);
        Assert.Equal(300_000, record.Usage.InputTokens);
        Assert.Equal(60_000, record.Usage.OutputTokens);
    }

    // Scenario: Pre-send local estimates are never mixed into cost totals
    [Fact]
    public void Pre_send_local_estimates_are_never_mixed_into_cost_totals()
    {
        // Given a pending message with a local pre-send estimate of 250000 tokens (not a completed turn)
        var conversationId = SeedConversation();
        SeedUserMessage(conversationId, tokens: 250_000);
        // And one completed assistant turn with authoritative usage (haiku input 1,000,000 = 1.00)
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 1_000_000, when: _clock.UtcNow);

        // When consolidated cost is computed
        var consolidated = _cost.GetProjectCost(_projectId);

        // Then the pending estimate is excluded from priced totals
        Assert.Equal(1.00m, consolidated.Total);
        // And only completed turns with authoritative usage are counted
        var record = Assert.Single(_cost.GetPricedTurns(_projectId));
        Assert.True(record.IsAuthoritative);
        Assert.Equal(1_000_000, record.Usage.InputTokens);
    }

    // ---- Optional budget guardrail (config, off by default) -----------------------------------

    // Scenario: A soft monthly budget shows a warning when exceeded and never blocks
    [Fact]
    public void A_soft_monthly_budget_shows_a_warning_when_exceeded_and_never_blocks()
    {
        // Given a project with a soft monthly budget of 10.00 USD enabled
        var budget = new ProjectBudget(Enabled: true, MonthlyLimitUsd: 10.00m);
        // And this month's consolidated cost is 8.00 USD (haiku input 8,000,000 this month)
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 8_000_000, when: _clock.UtcNow);

        // When a new turn completes costing 3.00 USD
        var evaluation = _cost.EvaluateBudget(_projectId, budget, newTurnCost: 3.00m);

        // Then a budget-exceeded warning is raised
        Assert.True(evaluation.Exceeded);
        Assert.Equal(8.00m, evaluation.MonthToDateCost);
        Assert.Equal(11.00m, evaluation.ProjectedCost);
        // And the turn is still recorded and not blocked (evaluation is a non-blocking warning)
        Assert.Equal(8.00m, _cost.GetProjectCost(_projectId).Total);
    }

    // Scenario: The budget guardrail is off by default
    [Fact]
    public void The_budget_guardrail_is_off_by_default()
    {
        // Given a new project with no budget configured
        // When this month's consolidated cost reaches 100.00 USD
        var conversationId = SeedConversation();
        SeedAssistantTurn(conversationId, "claude-haiku-4-5", input: 100_000_000, when: _clock.UtcNow);
        var evaluation = _cost.EvaluateBudget(_projectId, ProjectBudget.Off, newTurnCost: 0m);

        // Then no budget warning is raised
        Assert.False(evaluation.Exceeded);
    }

    // ---- seeding helpers ----------------------------------------------------------------------

    private void SeedProject(string projectId)
    {
        using var db = _store.CreateDbContext();
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Semiconductors 2026",
            Archived = false,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private string SeedConversation()
    {
        var id = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Conversations.Add(new Conversation
        {
            Id = id,
            ProjectId = _projectId,
            Title = "Thread",
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
        return id;
    }

    private string SeedAssistantTurn(
        string conversationId,
        string model,
        long input,
        DateTimeOffset when,
        long output = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        double? snapshotCost = null)
    {
        var id = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Messages.Add(new Message
        {
            Id = id,
            ConversationId = conversationId,
            Role = "assistant",
            Content = "answer",
            Model = model,
            TokensIn = input,
            TokensOut = output,
            TokensCacheRead = cacheRead,
            TokensCacheWrite = cacheWrite,
            CostUsd = snapshotCost,
            CreatedAt = when.ToString("o"),
        });
        db.SaveChanges();
        return id;
    }

    private void SeedUserMessage(string conversationId, long tokens)
    {
        using var db = _store.CreateDbContext();
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversationId,
            Role = "user",
            Content = "question",
            TokensIn = tokens,
            CreatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private void SeedArtifactVersion(string model, long input, DateTimeOffset when, long output = 0)
    {
        var artifactId = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            ProjectId = _projectId,
            Title = "Deliverable",
            Type = "doc",
            CreatedAt = when.ToString("o"),
            UpdatedAt = when.ToString("o"),
        });
        db.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid().ToString("N"),
            ArtifactId = artifactId,
            VersionNo = 1,
            Content = "content",
            Model = model,
            TokensIn = input,
            TokensOut = output,
            CreatedBy = "claude",
            CreatedAt = when.ToString("o"),
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp store.
        }
    }
}

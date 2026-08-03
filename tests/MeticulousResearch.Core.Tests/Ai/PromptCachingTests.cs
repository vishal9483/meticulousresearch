using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;
namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/prompt-caching/tests.md
/// (SPEC §8 stable context sent with cache breakpoints and metered; §3.6 cache tokens in per-turn
/// cost). These are @unit and run in the headless gate: request assembly and metering are driven end
/// to end through the scripted <see cref="FakeChatService"/> (echoing the breakpoints it received and
/// reporting scripted cache usage) over a temp SQLite database, with an <see cref="AdvancingClock"/>
/// keeping time deterministic. No network.
/// </summary>
public sealed class PromptCachingTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _service;

    public PromptCachingTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-prompt-caching-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _service = new ConversationService(_store, _chat, _projects, _resources, _clock);
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private static readonly IReadOnlyList<ChatResource> TwoResources = new[]
    {
        new ChatResource("A", "Filing", "Revenue was $1B"),
        new ChatResource("B", "Interview", "The CEO said growth is strong"),
    };

    // ---------------------------------------------------------- Breakpoint placement (§8)

    // @unit
    // Scenario: The system prompt (custom instructions) is sent with a cache breakpoint
    [Fact]
    public async Task The_system_prompt_is_sent_with_a_cache_breakpoint()
    {
        // Given a project with custom instructions
        var project = _projects.Create("P", customInstructions: "Formal tone; cite sources");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("ok").WithUsage(10, 5);

        // When a request is assembled for a turn
        await _service.Ask(conversation.Id, "q", "claude-opus-5", resourceScope: Array.Empty<ChatResource>());

        // Then the system prompt segment carries a cache breakpoint
        var request = Assert.IsType<ChatRequest>(_chat.LastRequest);
        Assert.Contains(request.CacheBreakpoints, b => b.Segment == ChatCacheSegment.System);
    }

    // @unit
    // Scenario: Stable enabled resource context is sent with a cache breakpoint
    [Fact]
    public async Task Stable_enabled_resource_context_is_sent_with_a_cache_breakpoint()
    {
        // Given a conversation with two stable enabled resources in scope
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("ok").WithUsage(10, 5);

        // When a request is assembled
        await _service.Ask(conversation.Id, "q", "claude-opus-5", resourceScope: TwoResources);

        // Then the resource context segment carries a cache breakpoint
        var request = Assert.IsType<ChatRequest>(_chat.LastRequest);
        Assert.Contains(request.CacheBreakpoints, b => b.Segment == ChatCacheSegment.Resources);
    }

    // @unit
    // Scenario: The volatile tail (history + new message) is not marked cacheable
    [Fact]
    public async Task The_volatile_tail_is_not_marked_cacheable()
    {
        // Given a request with cached system + resources (and prior history in the thread)
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("a1").WithUsage(10, 5);
        await _service.Ask(conversation.Id, "first question", "claude-opus-5", resourceScope: TwoResources);
        _chat.WithCompletionText("a2").WithUsage(10, 5);

        // When it is assembled
        await _service.Ask(conversation.Id, "second question", "claude-opus-5", resourceScope: TwoResources);

        // Then the new user message and recent history are outside the cache breakpoints
        var request = Assert.IsType<ChatRequest>(_chat.LastRequest);
        Assert.NotEmpty(request.History);                // recent history is present ...
        Assert.Equal("second question", request.UserMessage);
        // ... but only the two stable segments carry breakpoints — nothing marks the tail.
        Assert.All(request.CacheBreakpoints, b =>
            Assert.True(b.Segment is ChatCacheSegment.System or ChatCacheSegment.Resources));
        Assert.Equal(2, request.CacheBreakpoints.Count);
    }

    // ---------------------------------------------------------- Reuse across turns / regenerations (§8)

    // @unit
    // Scenario: A second turn with unchanged instructions and resources reuses the cache
    [Fact]
    public async Task A_second_turn_with_unchanged_context_reuses_the_cache()
    {
        // Given a first turn established the cache for the system prompt and resources
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("a1").WithUsage(input: 100, output: 20, cacheWrite: 1500);
        await _service.Ask(conversation.Id, "q1", "claude-opus-5", resourceScope: TwoResources);
        var first = _chat.LastRequest!.CacheBreakpoints;

        // When I ask a second question with the same instructions and resource scope
        _chat.WithCompletionText("a2").WithUsage(input: 20, output: 20, cacheRead: 1500);
        var second = await _service.Ask(conversation.Id, "q2", "claude-opus-5", resourceScope: TwoResources);

        // Then the second request presents the same cache breakpoints
        Assert.Equal(first, _chat.LastRequest!.CacheBreakpoints);
        // And the backend reports cache-read tokens on the second turn
        Assert.Equal(1500, second.TokensCacheRead);
    }

    // @unit
    // Scenario: Changing the enabled resource scope invalidates the resource cache segment
    [Fact]
    public async Task Changing_the_resource_scope_invalidates_the_resource_cache_segment()
    {
        // Given a cached resource segment for resources {A, B}
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("a1").WithUsage(100, 20, cacheWrite: 1500);
        await _service.Ask(conversation.Id, "q1", "claude-opus-5", resourceScope: TwoResources);
        var beforeKey = _chat.LastRequest!.CacheBreakpoints
            .Single(b => b.Segment == ChatCacheSegment.Resources).CacheKey;

        // When I change the scope to {A, C} and ask again
        var changedScope = new[]
        {
            new ChatResource("A", "Filing", "Revenue was $1B"),
            new ChatResource("C", "Memo", "New third-party memo"),
        };
        _chat.WithCompletionText("a2").WithUsage(100, 20, cacheWrite: 1500);
        await _service.Ask(conversation.Id, "q2", "claude-opus-5", resourceScope: changedScope);

        // Then the resource cache segment reflects the new scope
        var afterKey = _chat.LastRequest!.CacheBreakpoints
            .Single(b => b.Segment == ChatCacheSegment.Resources).CacheKey;

        // And is not served from the stale cache
        Assert.NotEqual(beforeKey, afterKey);
    }

    // @unit
    // Scenario: A regeneration (retry) of the same turn reuses the cached context
    [Fact]
    public async Task A_regeneration_retry_of_the_same_turn_reuses_the_cached_context()
    {
        // Given a completed turn with cached system + resources
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("a1").WithUsage(100, 20, cacheWrite: 1500);
        await _service.Ask(conversation.Id, "q1", "claude-opus-5", resourceScope: TwoResources);
        var original = _chat.LastRequest!.CacheBreakpoints;

        // When I retry the turn (same message, same instructions and resource scope)
        _chat.WithCompletionText("a1-again").WithUsage(input: 20, output: 20, cacheRead: 1500);
        var retry = await _service.Ask(conversation.Id, "q1", "claude-opus-5", resourceScope: TwoResources);

        // Then the retry request presents the same cache breakpoints
        Assert.Equal(original, _chat.LastRequest!.CacheBreakpoints);
        // And cache-read tokens are reported
        Assert.Equal(1500, retry.TokensCacheRead);
    }

    // ---------------------------------------------------------- Metering & cost (§8 / §3.6)

    // @unit
    // Scenario: Cache-read and cache-write tokens are recorded on the turn
    [Fact]
    public async Task Cache_read_and_write_tokens_are_recorded_on_the_turn()
    {
        // Given a backend that reports cache_write=1500 on the first turn and cache_read=1500 on the next
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);

        // When each turn completes
        _chat.WithCompletionText("a1").WithUsage(input: 100, output: 20, cacheWrite: 1500);
        var firstTurn = await _service.Ask(conversation.Id, "q1", "claude-opus-5", resourceScope: TwoResources);

        _chat.WithCompletionText("a2").WithUsage(input: 20, output: 20, cacheRead: 1500);
        var secondTurn = await _service.Ask(conversation.Id, "q2", "claude-opus-5", resourceScope: TwoResources);

        // Then the first turn records tokens_cache_write 1500
        Assert.Equal(1500, firstTurn.TokensCacheWrite);
        // And the second turn records tokens_cache_read 1500
        Assert.Equal(1500, secondTurn.TokensCacheRead);
    }

    // @unit
    // Scenario: Cache tokens are included in the per-turn cost using catalog cache rates
    [Fact]
    public async Task Cache_tokens_are_included_in_the_per_turn_cost_using_catalog_cache_rates()
    {
        // Given catalog cache-read and cache-write rates for the model
        var catalog = ModelCatalogLoader.Default;
        Assert.NotNull(catalog.GetPrice("claude-sonnet-5"));
        var calculator = new CatalogTurnCostCalculator(catalog);

        // And a turn reporting cache_read and cache_write tokens
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("ok").WithUsage(input: 1_000_000, output: 1_000_000,
            cacheRead: 1_000_000, cacheWrite: 1_000_000);
        var turn = await _service.Ask(conversation.Id, "q", "claude-sonnet-5", resourceScope: TwoResources);

        // When the per-turn cost is computed
        var breakdown = calculator.Calculate(TurnMetadata.FromMessage(turn));

        // Then the cost includes the cache-read and cache-write contributions
        Assert.True(breakdown.CacheReadCost > 0, "cache-read contribution should be included");
        Assert.True(breakdown.CacheWriteCost > 0, "cache-write contribution should be included");
        Assert.Equal(
            breakdown.InputCost + breakdown.OutputCost + breakdown.CacheReadCost + breakdown.CacheWriteCost,
            breakdown.Total, 6);
    }

    // @unit
    // Scenario: Missing cache usage records as zero, not error
    [Fact]
    public async Task Missing_cache_usage_records_as_zero_not_error()
    {
        // Given a backend that reports no cache fields
        var project = _projects.Create("P", customInstructions: "Formal tone");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("ok").WithUsage(input: 100, output: 20); // no cache fields

        // When a turn completes
        var turn = await _service.Ask(conversation.Id, "q", "claude-sonnet-5", resourceScope: TwoResources);

        // Then tokens_cache_read and tokens_cache_write are 0
        Assert.Equal(0, turn.TokensCacheRead);
        Assert.Equal(0, turn.TokensCacheWrite);

        // And the cost has no cache contribution
        var breakdown = new CatalogTurnCostCalculator(ModelCatalogLoader.Default)
            .Calculate(TurnMetadata.FromMessage(turn));
        Assert.Equal(0d, breakdown.CacheReadCost);
        Assert.Equal(0d, breakdown.CacheWriteCost);
    }
}

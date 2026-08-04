using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-09 — Prompt caching reduces repeat cost (covers SPEC §8, §3.6). The real conversation assembler
/// marks the stable system prompt and resource context with cache breakpoints; a follow-up that
/// reports cache-read tokens is priced at the catalog's cache rates, and the conversation running
/// cost reflects those cache-token effects.
/// </summary>
public sealed class J09_PromptCaching : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J09_PromptCaching() =>
        _projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Formal tone; cite sources").Id;

    public void Dispose() => _h.Dispose();

    // @e2e @unit
    // Scenario: A follow-up turn reuses cached system prompt and resource context
    [Fact]
    public async Task A_follow_up_turn_reuses_cached_system_prompt_and_resource_context()
    {
        _h.Resources.AddText(_projectId, "Filing", "Revenue was $1B in 2025.");
        var conversation = _h.Conversations.Create(_projectId);
        var scope = _h.EnabledScope(_projectId);

        // Given a first turn that establishes cache breakpoints on instructions + stable resources.
        _h.Chat.WithCompletionText("First answer.").WithUsage(1_000, 200, cacheRead: 0, cacheWrite: 1_000);
        await _h.Conversations.Ask(conversation.Id, "Establish context", "claude-opus-5", scope);
        var costAfterFirst = _h.Cost.GetConversationCost(conversation.Id).Cost;

        // When I ask a follow-up in the same conversation (the second turn reports cache-read tokens).
        _h.Chat.WithCompletionText("Follow-up answer.").WithUsage(50, 100, cacheRead: 1_000, cacheWrite: 0);
        await _h.Conversations.Ask(conversation.Id, "Follow up", "claude-opus-5", scope);

        // Then the request marked the system prompt and stable resources as cacheable breakpoints.
        var request = Assert.IsType<ChatRequest>(_h.Chat.LastRequest);
        Assert.Contains(request.CacheBreakpoints, b => b.Segment == ChatCacheSegment.System);
        Assert.Contains(request.CacheBreakpoints, b => b.Segment == ChatCacheSegment.Resources);

        // And its cost reflects cache-read/cache-write rates from the (pinned) model catalog.
        var cacheReadCost = _h.Cost.ComputeTurnCost(
            new TurnUsage(50, 100, CacheReadTokens: 1_000, CacheWriteTokens: 0), "claude-opus-5").CacheReadCost;
        Assert.True(cacheReadCost > 0m);
        Assert.Equal(1_000m / 1_000_000m * 0.5m, cacheReadCost);

        // And the conversation running cost includes the cache token effects (it grew with the follow-up).
        Assert.True(_h.Cost.GetConversationCost(conversation.Id).Cost > costAfterFirst);
    }
}

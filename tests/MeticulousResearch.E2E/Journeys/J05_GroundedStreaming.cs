using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Models;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-05 — Hold a grounded, streaming conversation with model selection and per-turn cost
/// (covers SPEC §9.1: 4). Grounding, streaming deltas, stop→interrupted persistence, per-turn cost,
/// and per-conversation/per-message model selection all run headlessly over the real conversation +
/// streaming services and the scripted backend.
/// </summary>
public sealed class J05_GroundedStreaming : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J05_GroundedStreaming() =>
        _projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Formal tone; cite sources").Id;

    public void Dispose() => _h.Dispose();

    // @e2e
    // Scenario: Ana asks a grounded question and watches the answer stream (grounding + per-turn cost)
    [Fact]
    public async Task Ana_asks_a_grounded_question_with_per_turn_cost_model_and_scope()
    {
        // Given a project with enabled resources.
        _h.Resources.AddText(_projectId, "Filing", "The competitive landscape is fragmented.");
        var conversation = _h.Conversations.Create(_projectId);
        var scope = _h.EnabledScope(_projectId);

        // When I ask a grounded question on the selected model.
        _h.Chat.WithCompletionText("The competitive landscape is fragmented across niche vendors.")
            .WithUsage(900, 200);
        var assistant = await _h.Conversations.Ask(
            conversation.Id, "Summarize the competitive landscape from my sources", "claude-opus-5", scope);

        // Then the request was grounded in the project's custom instructions and enabled resources.
        Assert.Equal("Formal tone; cite sources", _h.Chat.LastContext!.CustomInstructions);
        Assert.Contains(_h.Chat.LastContext!.Resources, r => r.Title == "Filing");
        Assert.Equal("Summarize the competitive landscape from my sources", _h.Chat.LastContext!.UserMessage);

        // And the turn records the model used and its in-scope resources.
        Assert.Equal("claude-opus-5", assistant.Model);
        Assert.NotNull(assistant.ResourceScopeJson);

        // And the turn has a computed cost; the conversation running total reflects it.
        var conversationCost = _h.Cost.GetConversationCost(conversation.Id);
        Assert.True(conversationCost.Cost > 0m);
        Assert.Equal(1_100, _h.Cost.GetProjectCost(_projectId).TotalTokens);
    }

    // @e2e
    // Scenario: the assistant response streams token-by-token into the thread
    [Fact]
    public async Task The_assistant_response_streams_token_by_token()
    {
        var conversation = _h.Conversations.Create(_projectId);
        _h.Chat.WithTokens("The", " landscape", " is", " fragmented.").WithUsage(10, 20);

        var snapshots = new List<string>();
        var turn = await _h.Streaming.StreamAsk(
            conversation.Id, "Summarize", "claude-opus-5", onDelta: t => snapshots.Add(t.Text));

        // Then the answer arrived incrementally (more than one delta) and completed cleanly.
        Assert.True(snapshots.Count >= 2, "expected the answer to stream token-by-token");
        Assert.Equal(StreamingState.Completed, turn.State);
        Assert.Equal("The landscape is fragmented.", turn.Text);
    }

    // @e2e
    // Scenario: Stopping a generation persists a clean interrupted turn
    [Fact]
    public async Task Stopping_a_generation_persists_a_clean_interrupted_turn()
    {
        var conversation = _h.Conversations.Create(_projectId);
        _h.Chat.WithTokens("A", "B", "C", "D").WithUsage(1, 1);

        // When I press Stop after two tokens have arrived.
        using var cts = new CancellationTokenSource();
        var turn = await _h.Streaming.StreamAsk(
            conversation.Id, "q", "claude-opus-5",
            onDelta: t => { if (t.Text == "AB") cts.Cancel(); },
            cancellationToken: cts.Token);

        // Then streaming halted and the partial turn is persisted and marked interrupted (no data loss).
        Assert.True(turn.IsInterrupted);
        Assert.Equal("AB", turn.Text);
        Assert.NotNull(turn.PersistedMessageId);
        var messages = _h.Conversations.GetMessages(conversation.Id);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "q");
        Assert.Contains(messages, m => m.Role == "assistant" && m.Content == "AB");
    }

    // @e2e @unit
    // Scenario Outline: The model is selectable per conversation and overridable per message.
    // (Tier labels resolve to concrete model ids via the catalog; here we drive concrete ids so the
    // recorded model is asserted deterministically regardless of catalog tier mappings.)
    [Theory]
    [InlineData("claude-opus-5", "claude-haiku-4-5", "claude-haiku-4-5")]
    [InlineData("claude-sonnet-5", null, "claude-sonnet-5")]
    public async Task The_model_is_selectable_per_conversation_and_overridable_per_message(
        string conversationModel, string? messageOverride, string recordedModel)
    {
        var conversation = _h.Conversations.Create(_projectId);
        var selection = ModelSelection.ForNewConversation(conversationModel);

        _h.Chat.WithCompletionText("ok").WithUsage(1, 1);
        var turnModel = selection.ResolveForTurn(messageOverride);
        var assistant = await _h.Conversations.Ask(conversation.Id, "hello", turnModel);

        // Then the assistant turn records the resolved model.
        Assert.Equal(recordedModel, assistant.Model);
        Assert.Equal(recordedModel, _h.Chat.LastContext!.Model);

        // And a per-message override does not change the conversation default.
        Assert.Equal(conversationModel, selection.ConversationModelId);
    }
}

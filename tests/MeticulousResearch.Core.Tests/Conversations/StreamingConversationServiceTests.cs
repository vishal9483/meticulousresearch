using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Time;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Conversations;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/streaming/tests.md
/// (SPEC §3.3 token-by-token streaming + stop/cancel; §8 interrupted stream persisted/resumable/
/// marked interrupted). These are @unit and run in the headless gate; they touch a temp SQLite
/// database (TESTING-STRATEGY §4) and drive generation through the scripted
/// <see cref="FakeChatService"/> so timing is deterministic and there is no network.
/// </summary>
public sealed class StreamingConversationServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;

    public StreamingConversationServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-streaming-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
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

    private StreamingConversationService NewService(FakeChatService chat) =>
        new(_store, chat, _projects, _resources, _clock);

    private (string ConversationId, string ProjectId) NewConversation()
    {
        var project = _projects.Create("P");
        var conversation = new ConversationService(
                _store, new FakeChatService(), _projects, _resources, _clock)
            .Create(project.Id);
        return (conversation.Id, project.Id);
    }

    private IReadOnlyList<Message> Messages(string conversationId)
    {
        return new ConversationService(_store, new FakeChatService(), _projects, _resources, _clock)
            .GetMessages(conversationId);
    }

    // ---------------------------------------------------------------- Token-by-token streaming

    // Scenario: Tokens are appended to the assistant turn as they arrive
    [Fact]
    public async Task Tokens_are_appended_to_the_assistant_turn_as_they_arrive()
    {
        var (conversationId, _) = NewConversation();

        // Given a backend scripted to emit "Mar", "ket ", "size" then complete
        var chat = new FakeChatService().WithTokens("Mar", "ket ", "size").WithUsage(1, 1);
        var service = NewService(chat);

        // When I ask a question (capturing the turn's text after each token)
        var snapshots = new List<string>();
        var turn = await service.StreamAsk(
            conversationId, "q", "claude-opus-5", onDelta: t => snapshots.Add(t.Text));

        // Then the assistant turn's text shows "Mar", then "Market ", then "Market size" as tokens arrive
        Assert.Equal(new[] { "Mar", "Market ", "Market size" }, snapshots);

        // And the final persisted text is "Market size"
        var assistant = Assert.Single(Messages(conversationId).Where(m => m.Role == "assistant"));
        Assert.Equal("Market size", assistant.Content);
        Assert.Equal("Market size", turn.Text);
    }

    // Scenario: A streaming cursor/indicator is shown while tokens are arriving and clears on completion
    [Fact]
    public async Task A_streaming_indicator_is_shown_while_tokens_arrive_and_clears_on_completion()
    {
        var (conversationId, _) = NewConversation();

        // Given a backend that streams then completes
        var chat = new FakeChatService().WithTokens("a", "b").WithUsage(1, 1);
        var service = NewService(chat);

        // When the response is streaming
        var streamingWhileArriving = false;
        var turn = await service.StreamAsk(
            conversationId, "q", "claude-opus-5",
            onDelta: t => streamingWhileArriving = t.IsStreaming);

        // Then the turn is in a "streaming" state (observed while tokens arrived)
        Assert.True(streamingWhileArriving, "the turn should be in a streaming state while tokens arrive");

        // And when it completes the "streaming" state clears
        Assert.False(turn.IsStreaming);
        Assert.Equal(StreamingState.Completed, turn.State);
    }

    // ---------------------------------------------------------------- Stop / cancel

    // Scenario: Stopping a stream halts token delivery
    [Fact]
    public async Task Stopping_a_stream_halts_token_delivery()
    {
        var (conversationId, _) = NewConversation();

        // Given a response is streaming (four tokens are scripted)
        var chat = new FakeChatService().WithTokens("A", "B", "C", "D").WithUsage(1, 1);
        var service = NewService(chat);

        // When I stop the generation (after two tokens have arrived)
        using var cts = new CancellationTokenSource();
        var turn = await service.StreamAsk(
            conversationId, "q", "claude-opus-5",
            onDelta: t => { if (t.Text == "AB") cts.Cancel(); },
            cancellationToken: cts.Token);

        // Then no further tokens are appended (only "A" and "B" made it in — not "C"/"D")
        Assert.Equal("AB", turn.Text);

        // And the turn is no longer in a "streaming" state
        Assert.False(turn.IsStreaming);
        Assert.Equal(StreamingState.Interrupted, turn.State);
    }

    // Scenario: A stopped turn persists the partial text and is marked interrupted
    [Fact]
    public async Task A_stopped_turn_persists_the_partial_text_and_is_marked_interrupted()
    {
        var (conversationId, _) = NewConversation();

        // Given a response has streamed "The market is grow" when I stop it
        var chat = new FakeChatService().WithTokens("The market is grow", "ing at 8% CAGR").WithUsage(1, 1);
        var service = NewService(chat);

        using var cts = new CancellationTokenSource();
        var turn = await service.StreamAsk(
            conversationId, "q", "claude-opus-5",
            onDelta: t => { if (t.Text == "The market is grow") cts.Cancel(); },
            cancellationToken: cts.Token);

        // Then the assistant turn is persisted with text "The market is grow"
        var assistant = Assert.Single(Messages(conversationId).Where(m => m.Role == "assistant"));
        Assert.Equal("The market is grow", assistant.Content);

        // And the turn is marked interrupted
        Assert.True(turn.IsInterrupted);
        Assert.Equal(assistant.Id, turn.PersistedMessageId);

        // And no work is lost (the user turn and the partial assistant turn are both durable)
        var messages = Messages(conversationId);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "q");
        Assert.Contains(messages, m => m.Role == "assistant" && m.Content == "The market is grow");
    }

    // ---------------------------------------------------------------- Interruption & resume (§8)

    // Scenario: A backend interruption mid-stream persists the partial turn marked interrupted
    [Fact]
    public async Task A_backend_interruption_mid_stream_persists_the_partial_turn_marked_interrupted()
    {
        var (conversationId, _) = NewConversation();

        // Given a backend that emits "Segment A: " then faults with a retryable error
        var chat = new FakeChatService()
            .WithTokens("Segment A: ")
            .FailWith(ChatErrorKind.Transport, retryable: true, "backend interrupted");
        var service = NewService(chat);

        // When I ask a question
        var turn = await service.StreamAsk(conversationId, "q", "claude-opus-5");

        // Then the assistant turn is persisted with the partial text "Segment A: "
        var assistant = Assert.Single(Messages(conversationId).Where(m => m.Role == "assistant"));
        Assert.Equal("Segment A: ", assistant.Content);

        // And the turn is marked interrupted
        Assert.True(turn.IsInterrupted);

        // And the failure is surfaced as retryable, not a lost turn
        Assert.NotNull(turn.Fault);
        Assert.True(turn.IsRetryable);
        Assert.Equal("Segment A: ", turn.Text);
    }

    // Scenario: An interrupted turn can be resumed
    [Fact]
    public async Task An_interrupted_turn_can_be_resumed()
    {
        var (conversationId, _) = NewConversation();

        // Given an interrupted assistant turn with partial text "Segment A: "
        var faulting = new FakeChatService()
            .WithTokens("Segment A: ")
            .FailWith(ChatErrorKind.Transport, retryable: true, "backend interrupted");
        var turn = await NewService(faulting).StreamAsk(conversationId, "q", "claude-opus-5");
        Assert.True(turn.IsInterrupted);
        Assert.Equal("Segment A: ", turn.Text);

        // When I resume it and the backend continues with "growing at 8% CAGR"
        var continuing = new FakeChatService().WithTokens("growing at 8% CAGR").WithUsage(1, 1);
        var resumed = await NewService(continuing).Resume(turn);

        // Then the turn's text becomes "Segment A: growing at 8% CAGR"
        Assert.Equal("Segment A: growing at 8% CAGR", resumed.Text);

        // And the turn is no longer marked interrupted
        Assert.False(resumed.IsInterrupted);
        Assert.Equal(StreamingState.Completed, resumed.State);

        // And the persisted turn now holds the continued full text (nothing lost, marker cleared)
        var assistant = Assert.Single(Messages(conversationId).Where(m => m.Role == "assistant"));
        Assert.Equal("Segment A: growing at 8% CAGR", assistant.Content);
    }

    // Scenario: Completing normally does not mark the turn interrupted
    [Fact]
    public async Task Completing_normally_does_not_mark_the_turn_interrupted()
    {
        var (conversationId, _) = NewConversation();

        // Given a backend that streams and completes cleanly
        var chat = new FakeChatService().WithTokens("all", " done").WithUsage(1, 1);
        var service = NewService(chat);

        // When the turn completes
        var turn = await service.StreamAsk(conversationId, "q", "claude-opus-5");

        // Then the turn is not marked interrupted
        Assert.False(turn.IsInterrupted);
        Assert.Equal(StreamingState.Completed, turn.State);
    }

    /// <summary>
    /// A monotonic <see cref="IClock"/> that advances by a fixed step on every read, so timestamps
    /// strictly increase and a measured latency is positive without wall-clock flakiness.
    /// </summary>
    private sealed class AdvancingClock : IClock
    {
        private DateTimeOffset _now;
        private readonly TimeSpan _step;

        public AdvancingClock(DateTimeOffset start, TimeSpan step)
        {
            _now = start;
            _step = step;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                var value = _now;
                _now += _step;
                return value;
            }
        }
    }
}

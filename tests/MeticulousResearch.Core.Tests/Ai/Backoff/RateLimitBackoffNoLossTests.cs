using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai.Backoff;

/// <summary>
/// Faithful @unit translation of the "do not lose work" scenarios in
/// docs/features/rate-limit-backoff/tests.md (SPEC §8 / §9.1(8)). These drive the real
/// <see cref="StreamingConversationService"/> over a temp SQLite database (TESTING-STRATEGY §4) with
/// its <see cref="IChatService"/> wrapped in <see cref="RetryingChatService"/>, proving persistence
/// stays idempotent under retries and that an exhausted retry hands the partial off to the
/// interrupted-persist/manual-retry path — with no real waiting.
/// </summary>
public sealed class RateLimitBackoffNoLossTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;

    public RateLimitBackoffNoLossTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-backoff-tests", Guid.NewGuid().ToString("N"));
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

    private static ChatFaulted StatusFault(int statusCode, string message = "scripted failure")
    {
        var (kind, retryable) = ChatErrorClassifier.FromStatusCode(statusCode);
        return new ChatFaulted(kind, retryable, message);
    }

    private RetryingChatService Backoff(SequencedChatService backend, int maxAttempts) =>
        new(backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()));

    private StreamingConversationService NewService(IChatService chat) =>
        new(_store, chat, _projects, _resources, _clock);

    private string NewConversation()
    {
        var project = _projects.Create("P");
        return new ConversationService(_store, new FakeChatService(), _projects, _resources, _clock)
            .Create(project.Id).Id;
    }

    private IReadOnlyList<Message> Messages(string conversationId) =>
        new ConversationService(_store, new FakeChatService(), _projects, _resources, _clock)
            .GetMessages(conversationId);

    // Scenario: A retry does not duplicate or discard the user's message
    [Fact]
    public async Task A_retry_does_not_duplicate_or_discard_the_users_message()
    {
        var conversationId = NewConversation();

        // Given a user message that triggers a 429 then succeeds on retry
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { new ChatCompleted("The market grew", ChatUsage.Zero) });
        var service = NewService(Backoff(backend, maxAttempts: 5));

        // When the generation completes
        var turn = await service.StreamAsk(conversationId, "How did the market do?", "claude-opus-5");

        Assert.Equal(2, backend.AskCount);
        Assert.Equal(StreamingState.Completed, turn.State);

        // Then exactly one user message is persisted
        var messages = Messages(conversationId);
        Assert.Single(messages.Where(m => m.Role == "user"));

        // And exactly one assistant message is persisted
        var assistant = Assert.Single(messages.Where(m => m.Role == "assistant"));
        Assert.Equal("The market grew", assistant.Content);
    }

    // Scenario: Retries stop after a maximum and preserve partial work
    [Fact]
    public async Task Retries_stop_after_a_maximum_and_preserve_partial_work()
    {
        var conversationId = NewConversation();

        // Given a backend that returns 429 on every attempt up to the retry limit
        // (each attempt streams a partial "Half " before faulting, so there is partial work to keep).
        const int maxAttempts = 3;
        var backend = new SequencedChatService(
            new ChatEvent[] { new ChatTokenDelta("Half "), StatusFault(429) },
            new ChatEvent[] { new ChatTokenDelta("Half "), StatusFault(429) },
            new ChatEvent[] { new ChatTokenDelta("Half "), StatusFault(429) });
        var service = NewService(Backoff(backend, maxAttempts));

        // When the limit is reached
        var turn = await service.StreamAsk(conversationId, "q", "claude-opus-5");

        // Then the generation stops retrying (no attempts beyond the cap)
        Assert.Equal(maxAttempts, backend.AskCount);

        // And any partial streamed text is persisted and marked interrupted
        Assert.Equal(StreamingState.Interrupted, turn.State);
        Assert.True(turn.IsInterrupted);
        Assert.Equal("Half ", turn.Text);
        var assistant = Assert.Single(Messages(conversationId).Where(m => m.Role == "assistant"));
        Assert.Equal("Half ", assistant.Content);

        // And the user is offered to retry manually (the interrupting fault is retryable → resumable)
        Assert.True(turn.IsRetryable);

        // And nothing is lost (the user message and the partial assistant message both survive)
        Assert.Single(Messages(conversationId).Where(m => m.Role == "user"));
        Assert.Equal(assistant.Id, turn.PersistedMessageId);
    }
}

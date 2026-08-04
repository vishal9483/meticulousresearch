using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.E2E.Support;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-08 — Rate-limit backoff without losing work (covers SPEC §9.1: 8, §8). A 429 mid-generation
/// drives a visible "retrying…" state, honours retry-after with exponential backoff + jitter, then
/// succeeds and persists the turn with no lost input — all deterministic (no real waiting) via the
/// scripted sequenced backend, a recording delay seam, and a fixed jitter source.
/// </summary>
public sealed class J08_RateLimitBackoff : IDisposable
{
    private readonly JourneyHarness _h = new();

    public void Dispose() => _h.Dispose();

    private sealed class RecordingObserver : IRetryObserver
    {
        public List<RetryState> Retries { get; } = new();
        public int Resolved { get; private set; }
        public void OnRetrying(RetryState state) => Retries.Add(state);
        public void OnResolved() => Resolved++;
    }

    private static ChatFaulted StatusFault(int statusCode)
    {
        var (kind, retryable) = ChatErrorClassifier.FromStatusCode(statusCode);
        return new ChatFaulted(kind, retryable, $"HTTP {statusCode}") { RetryAfter = TimeSpan.FromMilliseconds(20) };
    }

    private static RetryingChatService NewRetrying(
        IChatService backend, IRetryObserver observer, RecordingRetryDelay delay) =>
        new(backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            delay,
            observer);

    // @e2e
    // Scenario: A 429 mid-generation triggers visible retry, then succeeds
    [Fact]
    public async Task A_429_mid_generation_triggers_visible_retry_then_succeeds()
    {
        // Given the AI backend is scripted to return 429 with a retry-after, then succeed.
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { new ChatCompleted("The final answer.", ChatUsage.Zero) });
        var observer = new RecordingObserver();
        var delay = new RecordingRetryDelay(new FakeClock());
        var conversations = new ConversationService(
            _h.Store, NewRetrying(backend, observer, delay), _h.Projects, _h.Resources, _h.Clock);

        var project = _h.Projects.Create("EV Market 2026");
        var conversation = conversations.Create(project.Id);

        // When I send a message.
        var assistant = await conversations.Ask(conversation.Id, "What is the market size?", "claude-opus-5");

        // Then the app showed a "retrying…" state with the attempt count and honoured backoff (a delay
        // was waited before the successful retry).
        Assert.NotEmpty(observer.Retries);
        Assert.Equal(1, observer.Retries[0].Attempt);
        Assert.NotEmpty(delay.Delays);

        // When the retry succeeds, the final answer persists normally and no input was lost.
        Assert.Equal("The final answer.", assistant.Content);
        var messages = conversations.GetMessages(conversation.Id);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "What is the market size?");
        Assert.Contains(messages, m => m.Role == "assistant" && m.Content == "The final answer.");
    }

    // @e2e @unit
    // Scenario Outline: The gateway retries transient failures and surfaces terminal ones
    [Theory]
    [InlineData("429,429,200", "success", 3)]
    [InlineData("503,200", "success", 2)]
    [InlineData("401", "auth-error", 1)]
    public async Task The_gateway_retries_transient_failures_and_surfaces_terminal_ones(
        string sequence, string outcome, int attempts)
    {
        // Given the backend returns <sequence>.
        var scripted = sequence
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Select(code => code == 200
                ? new ChatEvent[] { new ChatCompleted("ok", ChatUsage.Zero) }
                : new ChatEvent[] { StatusFault(code) })
            .ToArray();
        var backend = new SequencedChatService(scripted);
        var retrying = NewRetrying(backend, new RecordingObserver(), new RecordingRetryDelay(new FakeClock()));

        // When a generation runs (drain the event stream).
        var events = new List<ChatEvent>();
        await foreach (var evt in retrying.Ask(new ChatAskContext { Model = "claude-opus-5", UserMessage = "q" }))
            events.Add(evt);

        // Then the outcome is <outcome> after <attempts> attempt(s).
        Assert.Equal(attempts, backend.AskCount);
        if (outcome == "success")
            Assert.Contains(events, e => e is ChatCompleted);
        else
            Assert.Contains(events, e => e is ChatFaulted { Retryable: false });
    }
}

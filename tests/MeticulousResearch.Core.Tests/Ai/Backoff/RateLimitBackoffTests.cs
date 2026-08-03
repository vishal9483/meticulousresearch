using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai.Backoff;

/// <summary>
/// Faithful @unit translation of the automatic-backoff, retry-after, and mid-stream-resume scenarios
/// in docs/features/rate-limit-backoff/tests.md (SPEC §8). Generation is driven through the scripted
/// <see cref="SequencedChatService"/> and all timing goes through an injected
/// <see cref="RecordingRetryDelay"/>/<see cref="FakeClock"/> + <see cref="FixedJitterSource"/>, so the
/// backoff is fully deterministic and no real time is waited (TESTING-STRATEGY §4).
/// </summary>
public sealed class RateLimitBackoffTests
{
    private static ChatFaulted StatusFault(int statusCode, string message = "scripted failure", TimeSpan? retryAfter = null)
    {
        var (kind, retryable) = ChatErrorClassifier.FromStatusCode(statusCode);
        return new ChatFaulted(kind, retryable, message) { RetryAfter = retryAfter };
    }

    private static ChatAskContext Context() => new() { Model = "claude-opus-5", UserMessage = "q" };

    private static async Task<List<ChatEvent>> Drain(IChatService service)
    {
        var events = new List<ChatEvent>();
        await foreach (var ev in service.Ask(Context()))
            events.Add(ev);
        return events;
    }

    // Scenario: A 429 is retried automatically and then succeeds
    [Fact]
    public async Task A_429_is_retried_automatically_and_then_succeeds()
    {
        // Given a backend scripted to return 429 once, then succeed
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { new ChatCompleted("The answer", ChatUsage.Zero) });
        var service = new RetryingChatService(
            backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()));

        // When I ask a question
        var events = await Drain(service);

        // Then the request is retried automatically
        Assert.Equal(2, backend.AskCount);

        // And the generation ultimately succeeds
        var completed = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal("The answer", completed.Text);

        // And I never see a hard failure
        Assert.DoesNotContain(events, e => e is ChatFaulted);
    }

    // Scenario Outline: Transient 5xx errors are retried like 429
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(529)]
    public async Task Transient_5xx_errors_are_retried_like_429(int status)
    {
        // Given a backend scripted to return "<status>" once, then succeed
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(status) },
            new ChatEvent[] { new ChatCompleted("Recovered", ChatUsage.Zero) });
        var service = new RetryingChatService(
            backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()));

        // When I ask a question
        var events = await Drain(service);

        // Then the request is retried automatically and succeeds
        Assert.Equal(2, backend.AskCount);
        Assert.DoesNotContain(events, e => e is ChatFaulted);
        Assert.Equal("Recovered", Assert.IsType<ChatCompleted>(events[^1]).Text);
    }

    // Scenario: Backoff grows exponentially with jitter across attempts
    [Fact]
    public async Task Backoff_grows_exponentially_with_jitter_across_attempts()
    {
        // Given a backend that returns 429 three times, then succeeds
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { StatusFault(429) },
            new ChatEvent[] { new ChatCompleted("Done", ChatUsage.Zero) });

        // And a deterministic jitter source
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var jitter = new FixedJitterSource(0.5);
        var delay = new RecordingRetryDelay(new FakeClock());
        var service = new RetryingChatService(
            backend, new BackoffPolicy(baseDelay, maxAttempts: 5, jitter), delay);

        // When I ask a question
        var events = await Drain(service);
        Assert.Equal("Done", Assert.IsType<ChatCompleted>(events[^1]).Text);

        // Then the delay before each retry increases roughly exponentially
        Assert.Equal(3, delay.Delays.Count);
        for (var i = 1; i < delay.Delays.Count; i++)
        {
            Assert.True(delay.Delays[i] > delay.Delays[i - 1],
                $"delay #{i + 1} ({delay.Delays[i]}) should exceed delay #{i} ({delay.Delays[i - 1]})");
            var ratio = delay.Delays[i].TotalMilliseconds / delay.Delays[i - 1].TotalMilliseconds;
            Assert.InRange(ratio, 1.5, 2.5); // ~doubles each attempt
        }

        // And a jitter component is applied to each delay
        Assert.Equal(delay.Delays.Count, jitter.CallCount);
        for (var i = 0; i < delay.Delays.Count; i++)
        {
            // The deterministic (no-jitter) half of the exponential value; a real jitter component
            // must push the delay strictly beyond it.
            var exponentialHalfMs = baseDelay.TotalMilliseconds * Math.Pow(2, i) / 2.0;
            Assert.True(delay.Delays[i].TotalMilliseconds > exponentialHalfMs,
                $"delay #{i + 1} ({delay.Delays[i].TotalMilliseconds}ms) should include jitter beyond the {exponentialHalfMs}ms baseline");
        }
    }

    // Scenario: A non-retryable error (e.g. 401) is not retried
    [Fact]
    public async Task A_non_retryable_error_401_is_not_retried()
    {
        // Given a backend scripted to return 401 Unauthorized
        const string message = "Your API key is invalid. Add a valid key in Settings.";
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(401, message) });
        var service = new RetryingChatService(
            backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()));

        // When I ask a question
        var events = await Drain(service);

        // Then the request is not retried
        Assert.Equal(1, backend.AskCount);

        // And I see a clear, actionable error (invalid key)
        var fault = Assert.IsType<ChatFaulted>(events[^1]);
        Assert.False(fault.Retryable);
        Assert.Contains("invalid", fault.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("key", fault.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Scenario: When retry-after is present, the wait honors it
    [Fact]
    public async Task When_retry_after_is_present_the_wait_honors_it()
    {
        // Given a backend that returns 429 with retry-after 7 seconds, then succeeds
        var backend = new SequencedChatService(
            new ChatEvent[] { StatusFault(429, retryAfter: TimeSpan.FromSeconds(7)) },
            new ChatEvent[] { new ChatCompleted("Finally", ChatUsage.Zero) });
        var clock = new FakeClock();
        var start = clock.UtcNow;
        var delay = new RecordingRetryDelay(clock);
        var service = new RetryingChatService(
            backend,
            // A tiny base delay so the computed backoff is far below 7s; only retry-after can raise it.
            new BackoffPolicy(TimeSpan.FromMilliseconds(50), maxAttempts: 5, new FixedJitterSource(0.0)),
            delay);

        // When I ask a question
        var events = await Drain(service);

        // Then the retry waits at least 7 seconds (per the injected clock)
        var waited = Assert.Single(delay.Delays);
        Assert.True(waited >= TimeSpan.FromSeconds(7), $"the wait ({waited}) should honor retry-after 7s");
        Assert.True(clock.UtcNow - start >= TimeSpan.FromSeconds(7), "the clock should have advanced at least 7s");

        // And then succeeds
        Assert.Equal("Finally", Assert.IsType<ChatCompleted>(events[^1]).Text);
    }

    // Scenario: retry-after overrides a shorter computed backoff
    [Fact]
    public void Retry_after_overrides_a_shorter_computed_backoff()
    {
        // Given a computed backoff of 2 seconds (base 2s, full jitter => exactly 2s for attempt 1)
        var policy = new BackoffPolicy(TimeSpan.FromSeconds(2), maxAttempts: 3, new FixedJitterSource(1.0));
        var computedBackoff = policy.ComputeDelay(1);
        Assert.Equal(TimeSpan.FromSeconds(2), computedBackoff);

        // And a 429 with retry-after 10 seconds
        var withRetryAfter = policy.ComputeDelay(1, TimeSpan.FromSeconds(10));

        // Then the wait is 10 seconds, not 2
        Assert.Equal(TimeSpan.FromSeconds(10), withRetryAfter);
        Assert.NotEqual(TimeSpan.FromSeconds(2), withRetryAfter);
    }

    // Scenario: A 429 mid-stream resumes without losing already-streamed tokens
    [Fact]
    public async Task A_429_mid_stream_resumes_without_losing_already_streamed_tokens()
    {
        // Given a stream that emits "The market " then hits a 429
        var backend = new SequencedChatService(
            new ChatEvent[] { new ChatTokenDelta("The market "), StatusFault(429) },
            // When the backoff retry continues the generation
            new ChatEvent[] { new ChatTokenDelta("The market has grown"), new ChatCompleted("The market has grown", ChatUsage.Zero) });
        var service = new RetryingChatService(
            backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()));

        // When I ask a question
        var events = await Drain(service);

        // Then the already-streamed text "The market " is preserved (delivered once, not duplicated)
        var deltas = events.OfType<ChatTokenDelta>().Select(d => d.Text).ToList();
        Assert.Equal("The market ", deltas[0]);
        var delivered = string.Concat(deltas);
        Assert.Equal("The market has grown", delivered);
        Assert.Contains("The market ", delivered);

        // And the final answer includes it
        var completed = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal("The market has grown", completed.Text);
        Assert.Contains("The market ", completed.Text);
    }
}

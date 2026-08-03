using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// Faithful @unit translation of the "retrying…" state scenario in
/// docs/features/rate-limit-backoff/tests.md (SPEC §8). Drives the real
/// <see cref="RetryingChatService"/> through a scripted <see cref="SequencedChatService"/> with the
/// window-free <see cref="RetryStatusViewModel"/> wired as its observer, and asserts the UI-facing
/// state reflects the attempt count while retrying and clears on success — no waiting, no window.
/// </summary>
public sealed class RetryStatusViewModelTests
{
    private static ChatFaulted Fault429()
    {
        var (kind, retryable) = ChatErrorClassifier.FromStatusCode(429);
        return new ChatFaulted(kind, retryable, "rate limited");
    }

    // Scenario: The UI reflects a "retrying…" state with the current attempt number
    [Fact]
    public async Task The_UI_reflects_a_retrying_state_with_the_current_attempt_number()
    {
        // Given a backend that returns 429 twice, then succeeds
        var backend = new SequencedChatService(
            new ChatEvent[] { Fault429() },
            new ChatEvent[] { Fault429() },
            new ChatEvent[] { new ChatCompleted("Done", ChatUsage.Zero) });

        var status = new RetryStatusViewModel();
        var service = new RetryingChatService(
            backend,
            new BackoffPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 5, new FixedJitterSource(0.5)),
            new RecordingRetryDelay(new FakeClock()),
            status);

        // Capture the retry state observed at each attempt transition.
        var observedAttempts = new List<int>();
        var observedStatusTexts = new List<string>();
        status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RetryStatusViewModel.Attempt) && status.IsRetrying)
            {
                observedAttempts.Add(status.Attempt);
                observedStatusTexts.Add(status.StatusText);
            }
        };

        // When I ask a question
        var context = new ChatAskContext { Model = "claude-opus-5", UserMessage = "q" };
        await foreach (var _ in service.Ask(context))
        {
            // drain the stream
        }

        // Then I see a "retrying…" state on attempt 1
        // And a "retrying…" state on attempt 2 showing the attempt count
        Assert.Equal(new[] { 1, 2 }, observedAttempts);
        Assert.Contains("attempt 1", observedStatusTexts[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attempt 2", observedStatusTexts[1], StringComparison.OrdinalIgnoreCase);
        Assert.All(observedStatusTexts, t => Assert.Contains("Retrying", t, StringComparison.OrdinalIgnoreCase));

        // And the state clears when the generation succeeds
        Assert.False(status.IsRetrying);
        Assert.Equal("", status.StatusText);
    }
}

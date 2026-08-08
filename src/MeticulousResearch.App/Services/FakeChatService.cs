using System.Runtime.CompilerServices;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.App.Services;

/// <summary>
/// Deterministic, offline <see cref="IChatService"/> used only when the app is launched under the
/// FlaUI @ui harness (<c>METICULOUS_UI_FAKE_AI=1</c>). It streams a few token deltas then completes
/// with fixed usage so conversation/streaming/turn-action journeys are exercisable without a key or
/// network. Never registered in a normal run.
/// </summary>
internal sealed class FakeChatService : IChatService
{
    private readonly HashSet<string> _rateLimitedMessages = new();

    public async IAsyncEnumerable<ChatEvent> Ask(
        ChatAskContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // A prompt mentioning rate limits faults once with a retryable 429 so the backoff layer
        // surfaces the non-alarming "retrying…" indicator; the automatic retry then succeeds.
        var message = context.UserMessage ?? "";
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) && _rateLimitedMessages.Add(message))
        {
            yield return new ChatFaulted(ChatErrorKind.RateLimited, Retryable: true, Message: "rate limited");
            yield break;
        }

        var parts = new[] { "Based on the in-scope resources, ", "here is a grounded answer ", "to your question." };
        foreach (var part in parts)
        {
            // A small per-token delay so the @ui harness can observe the streaming state (live
            // indicator, Stop control) and cancel mid-stream deterministically.
            var cancelled = false;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled || cancellationToken.IsCancellationRequested)
            {
                yield return new ChatCancelled();
                yield break;
            }

            yield return new ChatTokenDelta(part);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            yield return new ChatCancelled();
            yield break;
        }

        yield return new ChatCompleted(string.Concat(parts), new ChatUsage(InputTokens: 1200, OutputTokens: 300));
    }
}

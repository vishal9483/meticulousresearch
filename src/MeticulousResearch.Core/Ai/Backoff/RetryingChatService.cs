using System.Runtime.CompilerServices;
using System.Text;

namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// Decorator over <see cref="IChatService"/> that makes generation dependable under load (SPEC §8):
/// it automatically retries a retryable fault (429 / transient 5xx, per <see cref="ChatFaulted.Retryable"/>)
/// with exponential backoff + jitter (<see cref="BackoffPolicy"/>), honors a <c>retry-after</c> hint,
/// caps attempts, and surfaces a non-alarming "retrying…" state (<see cref="IRetryObserver"/>) instead
/// of failing. Because it is a decorator, every backend and every consumer (conversations, streaming,
/// artifacts) inherits backoff for free. Work is never lost: tokens already streamed to the caller are
/// preserved across a mid-stream retry (the re-sent prefix is not duplicated), and when retries are
/// exhausted the accumulated partial is handed back via the terminal <see cref="ChatFaulted"/> so the
/// streaming layer can persist it interrupted and offer a manual retry.
/// </summary>
public sealed class RetryingChatService : IChatService
{
    private readonly IChatService _inner;
    private readonly BackoffPolicy _policy;
    private readonly IRetryDelay _delay;
    private readonly IRetryObserver? _observer;

    /// <summary>Wraps an inner chat service with the backoff policy, delay seam, and retry observer.</summary>
    /// <param name="inner">The underlying generation service to retry.</param>
    /// <param name="policy">The backoff policy (delays + attempt cap).</param>
    /// <param name="delay">The delay seam (real timer in production, deterministic in tests).</param>
    /// <param name="observer">Optional receiver of "retrying…" state transitions for the UI.</param>
    public RetryingChatService(
        IChatService inner,
        BackoffPolicy policy,
        IRetryDelay delay,
        IRetryObserver? observer = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _observer = observer;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> Ask(
        ChatAskContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Text already delivered to the caller across all attempts so a re-sent prefix on retry is
        // not re-yielded (SPEC §8: a mid-stream 429 resumes without losing already-streamed tokens).
        var delivered = new StringBuilder();
        var attemptsMade = 0;

        while (true)
        {
            attemptsMade++;
            var attemptText = new StringBuilder();
            ChatCompleted? completed = null;
            ChatFaulted? fault = null;
            var cancelled = false;

            await foreach (var ev in _inner.Ask(context, cancellationToken).ConfigureAwait(false))
            {
                if (ev is ChatTokenDelta token)
                {
                    attemptText.Append(token.Text);
                    if (attemptText.Length > delivered.Length)
                    {
                        var suffix = attemptText.ToString(delivered.Length, attemptText.Length - delivered.Length);
                        delivered.Append(suffix);
                        yield return new ChatTokenDelta(suffix);
                    }
                }
                else
                {
                    switch (ev)
                    {
                        case ChatCompleted c: completed = c; break;
                        case ChatFaulted f: fault = f; break;
                        case ChatCancelled: cancelled = true; break;
                    }
                }
            }

            if (cancelled)
            {
                _observer?.OnResolved();
                yield return new ChatCancelled();
                yield break;
            }

            if (completed is not null)
            {
                _observer?.OnResolved();
                yield return completed;
                yield break;
            }

            if (fault is not null)
            {
                if (fault.Retryable && attemptsMade < _policy.MaxAttempts)
                {
                    var nextDelay = _policy.ComputeDelay(attemptsMade, fault.RetryAfter);
                    _observer?.OnRetrying(new RetryState(attemptsMade, nextDelay));
                    await _delay.Wait(nextDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Non-retryable, or retries exhausted: surface the fault. Any partial already streamed
                // to the caller is preserved (the streaming layer persists it interrupted).
                _observer?.OnResolved();
                yield return fault;
                yield break;
            }

            // A well-formed stream always ends in a terminal event; guard against a malformed one.
            _observer?.OnResolved();
            yield break;
        }
    }
}

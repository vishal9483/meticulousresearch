using System.Runtime.CompilerServices;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// Scripted <see cref="IChatService"/> that replays a distinct event sequence per <see cref="Ask"/>
/// call, so tests can model a backend that faults on the first attempt(s) and then succeeds on a
/// later one (rate-limit-backoff, SPEC §8). Each element passed to the constructor is the ordered
/// list of <see cref="ChatEvent"/>s for one attempt (zero or more <see cref="ChatTokenDelta"/> then
/// a terminal event). Cancellation stops the current attempt. <see cref="AskCount"/> records how
/// many attempts the caller actually made.
/// </summary>
public sealed class SequencedChatService : IChatService
{
    private readonly Queue<IReadOnlyList<ChatEvent>> _attempts;

    /// <summary>Creates the service from one event sequence per successive attempt.</summary>
    /// <param name="attempts">The per-attempt event sequences, in call order.</param>
    public SequencedChatService(params IReadOnlyList<ChatEvent>[] attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        _attempts = new Queue<IReadOnlyList<ChatEvent>>(attempts);
    }

    /// <summary>The number of times <see cref="Ask"/> was invoked (i.e. attempts made).</summary>
    public int AskCount { get; private set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> Ask(
        ChatAskContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AskCount++;
        if (_attempts.Count == 0)
            throw new InvalidOperationException("SequencedChatService ran out of scripted attempts.");

        var attempt = _attempts.Dequeue();
        foreach (var ev in attempt)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield return new ChatCancelled();
                yield break;
            }

            yield return ev;
            await Task.Yield();
        }
    }
}

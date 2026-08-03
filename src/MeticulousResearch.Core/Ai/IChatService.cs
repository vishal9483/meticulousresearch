namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The single generation contract the rest of the app depends on (SPEC §7.1, §7.3). Every consumer
/// — conversations, streaming, model-selector, rate-limit-backoff, prompt-caching, image-attachments
/// — talks only to this interface and is unaware whether the Agent SDK sidecar or the C# direct-API
/// backend produced the answer. Implementations assemble the request (custom instructions + in-scope
/// resources + history + message) identically and stream a sequence of <see cref="ChatEvent"/>.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Asks a question, returning a stream of <see cref="ChatTokenDelta"/> items followed by exactly
    /// one terminal event (<see cref="ChatCompleted"/> with final text and usage,
    /// <see cref="ChatCancelled"/>, or <see cref="ChatFaulted"/>). Cancelling
    /// <paramref name="cancellationToken"/> stops the stream promptly and completes it in a cancelled
    /// state — no further tokens are delivered.
    /// </summary>
    IAsyncEnumerable<ChatEvent> Ask(ChatAskContext context, CancellationToken cancellationToken = default);
}

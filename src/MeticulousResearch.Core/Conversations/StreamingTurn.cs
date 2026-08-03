using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// A mutable view of a single assistant turn while it streams and after it terminates (SPEC §3.3,
/// §8). The streaming orchestrator appends each <see cref="ChatTokenDelta"/> to <see cref="Text"/>
/// and drives <see cref="State"/> through the <see cref="StreamingState"/> machine. On a clean
/// finish the turn is <see cref="StreamingState.Completed"/>; on a user stop or a backend fault it
/// is <see cref="StreamingState.Interrupted"/> and its accumulated partial <see cref="Text"/> is
/// persisted so no work is lost. A retryable fault is surfaced via <see cref="Fault"/> /
/// <see cref="IsRetryable"/> without discarding the turn.
/// </summary>
public sealed class StreamingTurn
{
    /// <summary>Creates a turn for the given conversation and model, initially <see cref="StreamingState.Streaming"/>.</summary>
    /// <param name="conversationId">The owning conversation id.</param>
    /// <param name="model">The model id generating this turn.</param>
    /// <param name="text">The initial accumulated text (empty for a fresh turn).</param>
    public StreamingTurn(string conversationId, string model, string text = "")
    {
        ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Text = text ?? "";
        State = StreamingState.Streaming;
    }

    /// <summary>The owning conversation id.</summary>
    public string ConversationId { get; }

    /// <summary>The model id generating this turn.</summary>
    public string Model { get; }

    /// <summary>The text accumulated so far (grows as each token delta arrives).</summary>
    public string Text { get; internal set; }

    /// <summary>The current lifecycle state.</summary>
    public StreamingState State { get; internal set; }

    /// <summary>The id of the persisted assistant <c>Message</c> row backing this turn, once persisted.</summary>
    public string? PersistedMessageId { get; internal set; }

    /// <summary>The terminal fault when the turn was interrupted by the backend, else <c>null</c>.</summary>
    public ChatFaulted? Fault { get; internal set; }

    /// <summary>Whether tokens are still arriving (drives the streaming cursor/indicator).</summary>
    public bool IsStreaming => State == StreamingState.Streaming;

    /// <summary>Whether the turn was stopped or faulted mid-stream and holds partial text.</summary>
    public bool IsInterrupted => State == StreamingState.Interrupted;

    /// <summary>Whether an interrupting fault is retryable (surfaced to rate-limit-backoff / retry).</summary>
    public bool IsRetryable => Fault?.Retryable == true;
}

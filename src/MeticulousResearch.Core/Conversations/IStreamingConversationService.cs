using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// Streaming generation for a conversation (SPEC §3.3, §8): drives the <see cref="IChatService"/>
/// token stream, appends each delta to a live <see cref="StreamingTurn"/>, lets the caller stop via
/// a cancellation token, and persists the assistant turn on every outcome — final text on a clean
/// completion, or the accumulated partial marked interrupted on a stop or a backend fault so nothing
/// is lost. An interrupted turn can be <see cref="Resume"/>d to continue the generation and clear
/// the interrupted marker. Layered on top of the <c>conversations</c> Ask flow; persistence and
/// grounding follow the same model.
/// </summary>
public interface IStreamingConversationService
{
    /// <summary>
    /// Asks a question and streams the reply. Persists the user message, assembles the grounded
    /// request, and consumes the <see cref="IChatService"/> stream, appending each token delta to
    /// the returned <see cref="StreamingTurn"/> and invoking <paramref name="onDelta"/> after each
    /// append (for incremental rendering). Cancelling <paramref name="cancellationToken"/> stops
    /// delivery promptly; the accumulated partial is persisted and the turn is marked interrupted.
    /// A backend fault likewise persists the partial, marks the turn interrupted, and records the
    /// retryable classification. On a clean finish the final text is persisted and the turn is
    /// <see cref="StreamingState.Completed"/> (never interrupted).
    /// </summary>
    /// <param name="conversationId">The conversation to ask in.</param>
    /// <param name="message">The new user message.</param>
    /// <param name="model">The model id to generate with.</param>
    /// <param name="onDelta">Optional callback invoked after each token is appended.</param>
    /// <param name="resourceScope">Explicit in-scope resources; <c>null</c> uses the enabled set.</param>
    /// <param name="cancellationToken">Stops (interrupts) the in-flight stream.</param>
    /// <returns>The terminal <see cref="StreamingTurn"/> (completed or interrupted).</returns>
    /// <exception cref="InvalidOperationException">The conversation or its project does not exist.</exception>
    Task<StreamingTurn> StreamAsk(
        string conversationId,
        string message,
        string model,
        Action<StreamingTurn>? onDelta = null,
        IReadOnlyList<ChatResource>? resourceScope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an <see cref="StreamingState.Interrupted"/> turn: re-issues generation with the
    /// existing partial answer as context and appends the continued tokens to the same
    /// <see cref="StreamingTurn"/>. On a clean finish the persisted assistant message is updated to
    /// the full text and the interrupted marker is cleared; a further interruption re-marks it.
    /// </summary>
    /// <param name="turn">The interrupted turn to continue.</param>
    /// <param name="onDelta">Optional callback invoked after each appended token.</param>
    /// <param name="cancellationToken">Stops (re-interrupts) the resumed stream.</param>
    /// <returns>The updated <see cref="StreamingTurn"/>.</returns>
    /// <exception cref="InvalidOperationException">The turn is not interrupted, or its conversation is gone.</exception>
    Task<StreamingTurn> Resume(
        StreamingTurn turn,
        Action<StreamingTurn>? onDelta = null,
        CancellationToken cancellationToken = default);
}

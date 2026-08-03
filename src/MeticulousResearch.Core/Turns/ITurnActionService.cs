using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Turns;

/// <summary>
/// The per-turn action contract (SPEC §3.3) owned by <c>turn-metadata-actions</c>: retry (same or
/// another model), edit-and-resend, promote-to-artifact (request/provenance only — the artifact
/// domain is M3), and delete, plus the turn's <see cref="TurnMetadata"/> projection. Retry and
/// edit-and-resend reuse the <c>conversations</c> Ask path to regenerate the answer; delete removes
/// the turn from its conversation. Copy-to-clipboard is a view concern and lives in the App layer.
/// </summary>
public interface ITurnActionService
{
    /// <summary>Projects the metadata of the message with the given id.</summary>
    /// <param name="messageId">The assistant message id.</param>
    /// <exception cref="InvalidOperationException">The message does not exist.</exception>
    TurnMetadata GetMetadata(string messageId);

    /// <summary>
    /// Regenerates the answer to the assistant turn's question. The old user/assistant pair is
    /// superseded and a fresh turn is produced for the same question, preserving the original turn's
    /// in-scope resources. When <paramref name="modelOverride"/> is supplied the regeneration uses
    /// that model (the <c>model-selector</c> per-message override); otherwise the original model.
    /// </summary>
    /// <param name="assistantMessageId">The assistant turn to retry.</param>
    /// <param name="modelOverride">An optional other model id to regenerate with.</param>
    /// <param name="cancellationToken">Cancels the in-flight regeneration.</param>
    /// <returns>The new assistant <see cref="Message"/>.</returns>
    /// <exception cref="InvalidOperationException">The turn or its preceding user message is missing.</exception>
    Task<Message> Retry(
        string assistantMessageId,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the assistant turn's user message with <paramref name="newUserMessage"/> and
    /// regenerates, superseding the old assistant turn. Preserves the original in-scope resources and
    /// uses <paramref name="modelOverride"/> when supplied, else the original model.
    /// </summary>
    /// <param name="assistantMessageId">The assistant turn whose question is edited.</param>
    /// <param name="newUserMessage">The edited user message text.</param>
    /// <param name="modelOverride">An optional other model id to regenerate with.</param>
    /// <param name="cancellationToken">Cancels the in-flight regeneration.</param>
    /// <returns>The new assistant <see cref="Message"/>.</returns>
    /// <exception cref="ArgumentException">The edited message is null/blank.</exception>
    /// <exception cref="InvalidOperationException">The turn or its preceding user message is missing.</exception>
    Task<Message> EditAndResend(
        string assistantMessageId,
        string newUserMessage,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a <see cref="PromoteToArtifactRequest"/> from the assistant turn: the turn's content
    /// plus its provenance (source turn id, model, resource scope). Consumed by
    /// <c>artifact-creation</c> (M3); no artifact is created here.
    /// </summary>
    /// <param name="assistantMessageId">The assistant turn to promote.</param>
    /// <exception cref="InvalidOperationException">The turn does not exist.</exception>
    PromoteToArtifactRequest BuildPromoteRequest(string assistantMessageId);

    /// <summary>Removes the turn with the given id from its conversation. A no-op when it does not exist.</summary>
    /// <param name="messageId">The message (turn) to delete.</param>
    void Delete(string messageId);
}

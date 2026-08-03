using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// The conversation domain contract (SPEC §3.3, §5, §7.3): project-scoped Q&amp;A threads grounded
/// in the project's custom instructions and enabled resources. Owns the conversation/message model,
/// grounding assembly, and message persistence, and drives generation through the
/// <see cref="IChatService"/> gateway (owned by <c>ai-gateway</c>). Streaming UI, model selection,
/// and per-turn actions live in their own downstream features.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Creates a new, empty conversation in <paramref name="projectId"/> (untitled — a title is
    /// assigned automatically when its first turn completes).
    /// </summary>
    /// <param name="projectId">Owning project id.</param>
    /// <returns>The saved <see cref="Conversation"/>.</returns>
    /// <exception cref="ArgumentException">The project id is null/blank.</exception>
    Conversation Create(string projectId);

    /// <summary>Lists the conversations of <paramref name="projectId"/>, most recent first.</summary>
    IReadOnlyList<Conversation> List(string projectId);

    /// <summary>Returns the conversation with the given id, or <c>null</c> when it does not exist.</summary>
    Conversation? Get(string conversationId);

    /// <summary>
    /// Deletes a conversation and — by FK cascade — all of its messages. A no-op when the
    /// conversation does not exist.
    /// </summary>
    void Delete(string conversationId);

    /// <summary>Returns the conversation's messages in turn order (oldest first).</summary>
    IReadOnlyList<Message> GetMessages(string conversationId);

    /// <summary>
    /// Asks a question in a conversation (SPEC §7.3): persists the user message, assembles the
    /// grounded request (custom instructions + in-scope resources + ordered history + message),
    /// drives generation via <see cref="IChatService"/>, and on completion persists the assistant
    /// message with model, tokens, latency and the recorded resource scope, advances the
    /// conversation's <c>updated_at</c>, and auto-titles a still-untitled conversation.
    /// </summary>
    /// <param name="conversationId">The conversation to ask in.</param>
    /// <param name="message">The new user message.</param>
    /// <param name="model">The model id to generate with (supplied by <c>model-selector</c>).</param>
    /// <param name="resourceScope">
    /// The resources in scope for this turn. When <c>null</c> the project's currently enabled
    /// resources are used (disabled resources excluded); <c>context-budget</c> and the scope panel
    /// pass an explicit scope to adjust it.
    /// </param>
    /// <param name="cancellationToken">Cancels the in-flight turn.</param>
    /// <returns>The persisted assistant <see cref="Message"/>.</returns>
    /// <exception cref="InvalidOperationException">The conversation does not exist or the turn faulted.</exception>
    Task<Message> Ask(
        string conversationId,
        string message,
        string model,
        IReadOnlyList<ChatResource>? resourceScope = null,
        CancellationToken cancellationToken = default);
}

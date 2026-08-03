namespace MeticulousResearch.Core.Models;

/// <summary>
/// Per-conversation model selection state (SPEC §3.3): a conversation carries a selected model
/// (defaulting to the project default) that applies to its subsequent turns, and any single turn may
/// override it without mutating the conversation default. Model <em>ids</em> (not tier names) are
/// stored so historical turns stay valid if tiers are later re-pointed.
/// </summary>
public sealed class ModelSelection
{
    /// <summary>
    /// Creates a selection whose conversation default is <paramref name="conversationModelId"/>
    /// (typically the project default model for a new conversation).
    /// </summary>
    /// <param name="conversationModelId">The initial conversation model id.</param>
    /// <exception cref="ArgumentException">The model id is null/blank.</exception>
    public ModelSelection(string conversationModelId)
    {
        if (string.IsNullOrWhiteSpace(conversationModelId))
            throw new ArgumentException("A conversation model id is required.", nameof(conversationModelId));
        ConversationModelId = conversationModelId;
    }

    /// <summary>Starts a selection for a new conversation from the project's default model.</summary>
    /// <param name="projectDefaultModelId">The owning project's default model id.</param>
    public static ModelSelection ForNewConversation(string projectDefaultModelId) => new(projectDefaultModelId);

    /// <summary>The model applied to subsequent turns unless a per-turn override is supplied.</summary>
    public string ConversationModelId { get; private set; }

    /// <summary>
    /// Changes the conversation's model, applying to subsequent turns (does not affect turns already
    /// sent).
    /// </summary>
    /// <param name="modelId">The new conversation model id.</param>
    /// <exception cref="ArgumentException">The model id is null/blank.</exception>
    public void SetConversationModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("A model id is required.", nameof(modelId));
        ConversationModelId = modelId;
    }

    /// <summary>
    /// Resolves the model id to send for a turn: the <paramref name="perMessageOverride"/> when
    /// supplied (a one-off that does not change <see cref="ConversationModelId"/>), otherwise the
    /// conversation default.
    /// </summary>
    /// <param name="perMessageOverride">An optional one-turn override model id.</param>
    public string ResolveForTurn(string? perMessageOverride = null)
        => string.IsNullOrWhiteSpace(perMessageOverride) ? ConversationModelId : perMessageOverride!;
}

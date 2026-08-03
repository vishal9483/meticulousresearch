namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The inputs a consumer supplies to <see cref="IChatService.Ask"/> for one turn (SPEC §7.3):
/// the project's custom instructions, the selected model, the in-scope resources, the conversation
/// history, and the new user message. The gateway assembles these into a single
/// <see cref="ChatRequest"/> so both backends send identical payloads.
/// </summary>
public sealed record ChatAskContext
{
    /// <summary>The project's custom instructions, used as system context (null/empty when none).</summary>
    public string? CustomInstructions { get; init; }

    /// <summary>The model id selected for this turn (owned by <c>model-selector</c>).</summary>
    public required string Model { get; init; }

    /// <summary>The new user message.</summary>
    public required string UserMessage { get; init; }

    /// <summary>The enabled resources in scope for this turn, in order.</summary>
    public IReadOnlyList<ChatResource> Resources { get; init; } = Array.Empty<ChatResource>();

    /// <summary>The prior turns of the conversation, oldest first.</summary>
    public IReadOnlyList<ChatHistoryMessage> History { get; init; } = Array.Empty<ChatHistoryMessage>();
}

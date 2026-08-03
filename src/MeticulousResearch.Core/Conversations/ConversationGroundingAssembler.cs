using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// Pure §7.3 grounding assembler: turns a project's custom instructions, the in-scope resources,
/// the ordered conversation history, and the new user message into the single
/// <see cref="ChatAskContext"/> the <see cref="IChatService"/> gateway consumes. This is the one
/// place grounding assembly happens (shared with artifact generation later); it holds no state and
/// touches no I/O so it is trivially <c>@unit</c>-testable.
/// </summary>
public sealed class ConversationGroundingAssembler
{
    /// <summary>
    /// Builds the grounded turn context. <paramref name="customInstructions"/> becomes the system
    /// context, <paramref name="resources"/> the enabled grounding material (in order),
    /// <paramref name="history"/> the prior turns (oldest first), and
    /// <paramref name="userMessage"/> the new question.
    /// </summary>
    public ChatAskContext Assemble(
        string? customInstructions,
        string model,
        string userMessage,
        IReadOnlyList<ChatResource> resources,
        IReadOnlyList<ChatHistoryMessage> history,
        IReadOnlyList<ImageContentBlock>? userImages = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(userMessage);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(history);

        return new ChatAskContext
        {
            CustomInstructions = string.IsNullOrWhiteSpace(customInstructions) ? null : customInstructions,
            Model = model,
            UserMessage = userMessage,
            Resources = resources,
            History = history,
            UserImages = userImages ?? Array.Empty<ImageContentBlock>(),
        };
    }
}

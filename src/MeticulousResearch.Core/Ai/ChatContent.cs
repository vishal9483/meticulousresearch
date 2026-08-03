namespace MeticulousResearch.Core.Ai;

/// <summary>
/// An in-scope resource included in a request's grounding context. The gateway carries the
/// resource's identity plus its extracted text so both backends send the same grounding payload.
/// </summary>
/// <param name="Id">The resource id (for traceability / caching breakpoints).</param>
/// <param name="Title">The resource display title.</param>
/// <param name="Text">The resource's extracted text.</param>
public sealed record ChatResource(string Id, string Title, string Text);

/// <summary>A prior turn in the conversation history replayed to the backend.</summary>
/// <param name="Role">The role: <c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">The turn's text content.</param>
public sealed record ChatHistoryMessage(string Role, string Content);

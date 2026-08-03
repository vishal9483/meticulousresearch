namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The fully-assembled payload the gateway hands to a backend (SPEC §7.3). Assembly happens once,
/// in <see cref="ChatRequestAssembler"/>, so the sidecar and direct-API backends send byte-for-byte
/// equivalent requests. The resolved <see cref="ApiKey"/> and <see cref="BaseUrl"/> travel with the
/// request; the key is delivered to the sidecar over its authenticated channel, never its command line.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>The model id to generate with.</summary>
    public required string Model { get; init; }

    /// <summary>The system prompt (the project's custom instructions).</summary>
    public required string System { get; init; }

    /// <summary>The in-scope resources included as grounding context.</summary>
    public IReadOnlyList<ChatResource> Resources { get; init; } = Array.Empty<ChatResource>();

    /// <summary>The conversation history replayed to the backend, oldest first.</summary>
    public IReadOnlyList<ChatHistoryMessage> History { get; init; } = Array.Empty<ChatHistoryMessage>();

    /// <summary>The new user message.</summary>
    public required string UserMessage { get; init; }

    /// <summary>The resolved effective API key (env wins, else the secure store).</summary>
    public required string ApiKey { get; init; }

    /// <summary>The resolved effective base URL (env wins, else the setting, else the public API).</summary>
    public required string BaseUrl { get; init; }
}

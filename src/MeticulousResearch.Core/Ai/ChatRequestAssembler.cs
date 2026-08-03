namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Assembles a <see cref="ChatAskContext"/> plus the resolved credentials into a single
/// <see cref="ChatRequest"/> (SPEC §7.3). Centralizing assembly here guarantees the sidecar and
/// direct-API backends send identical payloads — the system prompt is the project's custom
/// instructions, followed by the in-scope resources, the history, and the new user message.
/// </summary>
public sealed class ChatRequestAssembler
{
    /// <summary>
    /// Builds the request the active backend will send. <paramref name="apiKey"/> and
    /// <paramref name="baseUrl"/> are the effective values resolved via <c>IApiCredentialProvider</c>.
    /// </summary>
    public ChatRequest Assemble(ChatAskContext context, string apiKey, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(baseUrl);

        return new ChatRequest
        {
            Model = context.Model,
            System = context.CustomInstructions ?? string.Empty,
            Resources = context.Resources,
            History = context.History,
            UserMessage = context.UserMessage,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        };
    }
}

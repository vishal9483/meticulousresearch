namespace MeticulousResearch.Core.Ai;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Assembles a <see cref="ChatAskContext"/> plus the resolved credentials into a single
/// <see cref="ChatRequest"/> (SPEC §7.3). Centralizing assembly here guarantees the sidecar and
/// direct-API backends send identical payloads — the system prompt is the project's custom
/// instructions, followed by the in-scope resources, the history, and the new user message.
/// It also makes the backend-agnostic prompt-caching decision (SPEC §8): the system prompt and the
/// stable enabled-resource context each carry a <see cref="CacheBreakpoint"/>, while the volatile
/// tail (history + new message) does not.
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

        var system = context.CustomInstructions ?? string.Empty;

        return new ChatRequest
        {
            Model = context.Model,
            System = system,
            Resources = context.Resources,
            History = context.History,
            UserMessage = context.UserMessage,
            UserImages = context.UserImages,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            CacheBreakpoints = BuildCacheBreakpoints(system, context.Resources),
        };
    }

    /// <summary>
    /// Places cache breakpoints on the stable segments (SPEC §8), most-stable-first: the system prompt
    /// when it carries content, then the enabled-resource context when in scope. The volatile tail is
    /// never marked. Each breakpoint's key is a stable digest of its exact content so an unchanged
    /// segment reuses the cache while a changed scope invalidates it.
    /// </summary>
    private static IReadOnlyList<CacheBreakpoint> BuildCacheBreakpoints(
        string system, IReadOnlyList<ChatResource> resources)
    {
        var breakpoints = new List<CacheBreakpoint>(2);

        if (!string.IsNullOrWhiteSpace(system))
            breakpoints.Add(new CacheBreakpoint(ChatCacheSegment.System, Digest(system)));

        if (resources.Count > 0)
            breakpoints.Add(new CacheBreakpoint(ChatCacheSegment.Resources, ResourceDigest(resources)));

        return breakpoints;
    }

    /// <summary>Builds a stable digest over the ordered resources' identity and extracted text.</summary>
    private static string ResourceDigest(IReadOnlyList<ChatResource> resources)
    {
        var builder = new StringBuilder();
        foreach (var resource in resources)
            builder.Append(resource.Id).Append('\u001f')
                   .Append(resource.Text).Append('\u001e');
        return Digest(builder.ToString());
    }

    /// <summary>Computes a stable, content-derived hex digest (SHA-256) for a cache segment.</summary>
    private static string Digest(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}

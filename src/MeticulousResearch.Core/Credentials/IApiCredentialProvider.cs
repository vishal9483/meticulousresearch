namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// The single place the rest of the app asks for the <b>effective</b> API key and base URL,
/// applying the env-wins resolution order from SPEC §7.5 (settings-secure-key/phase.md).
/// <c>ai-gateway</c> consumes this; nothing else reads the environment variable or the secure
/// store directly. An env-supplied value is read live and never persisted.
/// </summary>
public interface IApiCredentialProvider
{
    /// <summary>
    /// The effective API key: <c>ANTHROPIC_API_KEY</c> (if set and non-empty) → the secure store →
    /// <c>null</c> when none is configured.
    /// </summary>
    string? ResolveApiKey();

    /// <summary>True when an effective key is available from any source.</summary>
    bool HasApiKey { get; }

    /// <summary>True when the effective key comes from the <c>ANTHROPIC_API_KEY</c> environment variable.</summary>
    bool IsApiKeyFromEnvironment { get; }

    /// <summary>
    /// The effective base URL: <c>ANTHROPIC_BASE_URL</c> (if set and non-empty) → the persisted
    /// base-URL setting → the default public Anthropic API. A trailing slash is normalized away.
    /// </summary>
    string ResolveBaseUrl();

    /// <summary>True when the effective base URL comes from the <c>ANTHROPIC_BASE_URL</c> environment variable.</summary>
    bool IsBaseUrlFromEnvironment { get; }
}

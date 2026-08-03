namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// Canonical endpoint constants for the Anthropic API (settings-secure-key/phase.md, SPEC §7.5).
/// </summary>
public static class AnthropicApi
{
    /// <summary>The default public Anthropic API base URL (no trailing slash).</summary>
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    /// <summary>The environment variable that overrides the resolved API key.</summary>
    public const string ApiKeyEnvVar = "ANTHROPIC_API_KEY";

    /// <summary>The environment variable that overrides the resolved base URL.</summary>
    public const string BaseUrlEnvVar = "ANTHROPIC_BASE_URL";
}

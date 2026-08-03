using MeticulousResearch.Core.Environment;
using MeticulousResearch.Core.Security;
using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// Resolves the effective API key and base URL with the env-wins order from SPEC §7.5. The
/// environment is read live through an injected <see cref="IEnvironment"/> (never
/// <c>System.Environment</c> inline), and an env-supplied value is never written back to the
/// secure store or settings (settings-secure-key/phase.md).
/// </summary>
public sealed class ApiCredentialProvider : IApiCredentialProvider
{
    private readonly IEnvironment _environment;
    private readonly ISecureKeyStore _keyStore;
    private readonly ISettingsService _settings;

    /// <summary>Creates the provider over the environment, secure store, and settings.</summary>
    public ApiCredentialProvider(IEnvironment environment, ISecureKeyStore keyStore, ISettingsService settings)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public string? ResolveApiKey()
    {
        var envKey = _environment.GetEnvironmentVariable(AnthropicApi.ApiKeyEnvVar);
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        var stored = _keyStore.Get();
        return string.IsNullOrEmpty(stored) ? null : stored;
    }

    /// <inheritdoc />
    public bool HasApiKey => !string.IsNullOrEmpty(ResolveApiKey());

    /// <inheritdoc />
    public bool IsApiKeyFromEnvironment =>
        !string.IsNullOrEmpty(_environment.GetEnvironmentVariable(AnthropicApi.ApiKeyEnvVar));

    /// <inheritdoc />
    public string ResolveBaseUrl()
    {
        var envUrl = _environment.GetEnvironmentVariable(AnthropicApi.BaseUrlEnvVar);
        if (!string.IsNullOrEmpty(envUrl))
            return Normalize(envUrl);

        var setting = _settings.ApiBaseUrl;
        if (!string.IsNullOrEmpty(setting))
            return Normalize(setting!);

        return AnthropicApi.DefaultBaseUrl;
    }

    /// <inheritdoc />
    public bool IsBaseUrlFromEnvironment =>
        !string.IsNullOrEmpty(_environment.GetEnvironmentVariable(AnthropicApi.BaseUrlEnvVar));

    private static string Normalize(string url) => url.TrimEnd('/');
}

using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Resolves the active backend from <see cref="ISettingsService.ChatBackend"/>. A fresh installation
/// (no stored preference) resolves to the sidecar; the preference value <c>direct-api</c> selects the
/// direct-API fallback. Backends are supplied as factories so the inactive one is never constructed.
/// </summary>
public sealed class ChatBackendFactory : IChatBackendFactory
{
    /// <summary>The settings value that selects the direct-API fallback backend.</summary>
    public const string DirectApiPreference = "direct-api";

    /// <summary>The settings value that selects the sidecar backend (the default).</summary>
    public const string SidecarPreference = "sidecar";

    private readonly ISettingsService _settings;
    private readonly Func<IChatService> _sidecar;
    private readonly Func<IChatService> _directApi;

    /// <summary>Creates the factory over settings and the two backend factories.</summary>
    public ChatBackendFactory(ISettingsService settings, Func<IChatService> sidecar, Func<IChatService> directApi)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _directApi = directApi ?? throw new ArgumentNullException(nameof(directApi));
    }

    /// <inheritdoc />
    public ChatBackendKind Active =>
        string.Equals(_settings.ChatBackend, DirectApiPreference, StringComparison.OrdinalIgnoreCase)
            ? ChatBackendKind.DirectApi
            : ChatBackendKind.Sidecar;

    /// <inheritdoc />
    public IChatService Resolve() =>
        Active == ChatBackendKind.DirectApi ? _directApi() : _sidecar();
}

namespace MeticulousResearch.Core.Settings;

/// <summary>
/// Typed access to app-level (non-secret) preferences over the <c>Setting</c> table
/// (settings-secure-key/phase.md, SPEC §3.5). Secrets never live here — they go to
/// <c>ISecureKeyStore</c>. All setters persist immediately and raise <see cref="SettingsChanged"/>.
/// </summary>
public interface ISettingsService
{
    /// <summary>The default model id for new conversations. Defaults to <c>claude-opus-5</c>.</summary>
    string DefaultModel { get; set; }

    /// <summary>The selected theme name. Defaults to <c>System</c>.</summary>
    string Theme { get; set; }

    /// <summary>The context budget in tokens.</summary>
    int ContextBudget { get; set; }

    /// <summary>Whether anonymous telemetry is enabled. Off by default.</summary>
    bool TelemetryEnabled { get; set; }

    /// <summary>
    /// The persisted API base URL, or <c>null</c> when unset. The <c>ANTHROPIC_BASE_URL</c>
    /// environment variable overrides this at resolution time but is never written here.
    /// </summary>
    string? ApiBaseUrl { get; set; }

    /// <summary>The configured data directory, or <c>null</c> when the default is used.</summary>
    string? DataDirectory { get; set; }

    /// <summary>
    /// The selected generation backend preference consumed by <c>ai-gateway</c>: <c>sidecar</c>
    /// (the default) or <c>direct-api</c>. Resolved to a concrete backend by <c>IChatBackendFactory</c>.
    /// </summary>
    string ChatBackend { get; set; }

    /// <summary>Raised after any setting changes and is persisted.</summary>
    event EventHandler? SettingsChanged;
}

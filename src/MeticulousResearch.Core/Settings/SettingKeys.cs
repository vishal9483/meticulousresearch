namespace MeticulousResearch.Core.Settings;

/// <summary>
/// Canonical <c>Setting</c>-table keys for app-level (non-secret) preferences. Pinned so the
/// settings service and any downstream feature agree on storage keys (settings-secure-key/phase.md).
/// </summary>
public static class SettingKeys
{
    /// <summary>The default model id used for new conversations (model-selector reads this).</summary>
    public const string DefaultModel = "default_model";

    /// <summary>The selected theme (design-system-theming consumes this).</summary>
    public const string Theme = "theme";

    /// <summary>The context budget in tokens.</summary>
    public const string ContextBudget = "context_budget";

    /// <summary>Whether anonymous telemetry is enabled.</summary>
    public const string TelemetryEnabled = "telemetry_enabled";

    /// <summary>The persisted API base URL setting (env var wins over this at resolution time).</summary>
    public const string ApiBaseUrl = "api_base_url";

    /// <summary>The configured data directory.</summary>
    public const string DataDirectory = "data_directory";
}

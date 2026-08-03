using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.Core.Tests.Credentials;

/// <summary>
/// In-memory <see cref="ISettingsService"/> for credential-resolution @unit tests. Only the
/// base-URL setting matters here; the rest hold their defaults.
/// </summary>
internal sealed class StubSettings : ISettingsService
{
    public string DefaultModel { get; set; } = SettingsService.DefaultModelValue;
    public string Theme { get; set; } = SettingsService.DefaultThemeValue;
    public int ContextBudget { get; set; } = SettingsService.DefaultContextBudgetValue;
    public bool TelemetryEnabled { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? DataDirectory { get; set; }
    public string ChatBackend { get; set; } = SettingsService.DefaultChatBackendValue;

    public event EventHandler? SettingsChanged;

    public void RaiseChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}

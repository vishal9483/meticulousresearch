using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Settings;

/// <summary>
/// <see cref="ISettingsService"/> over the <c>Setting</c> key/value table (from
/// data-store-migrations). Values are loaded once on construction and written through on every
/// setter, so a fresh instance over the same <see cref="DataStore"/> observes persisted values
/// (settings-secure-key "settings persist across restart").
/// </summary>
public sealed class SettingsService : ISettingsService
{
    /// <summary>The default model id when none has been configured (SPEC §3.5).</summary>
    public const string DefaultModelValue = "claude-opus-5";

    /// <summary>The default theme selection when none has been configured.</summary>
    public const string DefaultThemeValue = "System";

    /// <summary>The default context budget in tokens when none has been configured (SPEC §6).</summary>
    public const int DefaultContextBudgetValue = 150_000;

    /// <summary>The default generation backend when none has been configured (SPEC §7.2): the sidecar.</summary>
    public const string DefaultChatBackendValue = "sidecar";

    private readonly DataStore _store;
    private readonly Dictionary<string, string?> _values;

    /// <summary>Creates the service and loads all persisted settings from the data store.</summary>
    public SettingsService(DataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _values = new Dictionary<string, string?>(StringComparer.Ordinal);

        using var db = _store.CreateDbContext();
        foreach (var s in db.Settings.AsNoTracking().ToList())
            _values[s.Key] = s.Value;
    }

    /// <inheritdoc />
    public event EventHandler? SettingsChanged;

    /// <inheritdoc />
    public string DefaultModel
    {
        get => GetString(SettingKeys.DefaultModel, DefaultModelValue);
        set => SetString(SettingKeys.DefaultModel, value);
    }

    /// <inheritdoc />
    public string Theme
    {
        get => GetString(SettingKeys.Theme, DefaultThemeValue);
        set => SetString(SettingKeys.Theme, value);
    }

    /// <inheritdoc />
    public int ContextBudget
    {
        get => _values.TryGetValue(SettingKeys.ContextBudget, out var v)
               && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : DefaultContextBudgetValue;
        set => SetString(SettingKeys.ContextBudget, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public bool TelemetryEnabled
    {
        get => _values.TryGetValue(SettingKeys.TelemetryEnabled, out var v)
               && bool.TryParse(v, out var b) && b;
        set => SetString(SettingKeys.TelemetryEnabled, value ? bool.TrueString : bool.FalseString);
    }

    /// <inheritdoc />
    public string? ApiBaseUrl
    {
        get => GetNullable(SettingKeys.ApiBaseUrl);
        set => SetString(SettingKeys.ApiBaseUrl, value);
    }

    /// <inheritdoc />
    public string? DataDirectory
    {
        get => GetNullable(SettingKeys.DataDirectory);
        set => SetString(SettingKeys.DataDirectory, value);
    }

    /// <inheritdoc />
    public string? DismissedUpdateVersion
    {
        get => GetNullable(SettingKeys.DismissedUpdateVersion);
        set => SetString(SettingKeys.DismissedUpdateVersion, value);
    }

    /// <inheritdoc />
    public string ChatBackend
    {
        get => GetString(SettingKeys.ChatBackend, DefaultChatBackendValue);
        set => SetString(SettingKeys.ChatBackend, value);
    }

    private string GetString(string key, string fallback) =>
        _values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v! : fallback;

    private string? GetNullable(string key) =>
        _values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    private void SetString(string key, string? value)
    {
        _values[key] = value;

        using var db = _store.CreateDbContext();
        var existing = db.Settings.FirstOrDefault(s => s.Key == key);
        if (existing is null)
            db.Settings.Add(new Setting { Key = key, Value = value });
        else
            existing.Value = value;
        db.SaveChanges();

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}

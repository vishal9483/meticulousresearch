using System.Text.Json;

namespace MeticulousResearch.Core.Theming;

/// <summary>
/// A local-setting <see cref="IThemeStore"/> that persists the selected theme to a small JSON
/// file so the choice survives an app restart. Superseded by <c>ISettingsService</c> once
/// settings-secure-key lands (design-system-theming/phase.md).
/// </summary>
public sealed class JsonFileThemeStore : IThemeStore
{
    private readonly string _path;

    /// <summary>Creates a store backed by the given file path.</summary>
    public JsonFileThemeStore(string path) =>
        _path = path ?? throw new ArgumentNullException(nameof(path));

    /// <inheritdoc />
    public AppTheme? Load()
    {
        if (!File.Exists(_path))
            return null;
        try
        {
            var json = File.ReadAllText(_path);
            var model = JsonSerializer.Deserialize<PersistedTheme>(json);
            if (model?.Theme is { } name && Enum.TryParse<AppTheme>(name, out var theme))
                return theme;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt/unreadable setting falls back to the default selection.
        }
        return null;
    }

    /// <inheritdoc />
    public void Save(AppTheme theme)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(new PersistedTheme { Theme = theme.ToString() });
        File.WriteAllText(_path, json);
    }

    private sealed class PersistedTheme
    {
        public string? Theme { get; set; }
    }
}

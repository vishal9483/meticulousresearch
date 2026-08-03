using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// In-memory <see cref="ISystemThemeProvider"/> for tests. The OS theme can be set and changed so
/// System-theme resolution and live OS-change reactions are exercised deterministically.
/// </summary>
public sealed class FakeSystemThemeProvider : ISystemThemeProvider
{
    private AppTheme _systemTheme;

    /// <summary>Creates a provider reporting the given OS theme (Light by default).</summary>
    public FakeSystemThemeProvider(AppTheme systemTheme = AppTheme.Light) =>
        _systemTheme = systemTheme;

    public AppTheme GetSystemTheme() => _systemTheme;

    public event EventHandler? SystemThemeChanged;

    /// <summary>Simulates the user changing the OS light/dark preference.</summary>
    public void SetSystemTheme(AppTheme theme)
    {
        _systemTheme = theme;
        SystemThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}

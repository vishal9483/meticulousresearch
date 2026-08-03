namespace MeticulousResearch.Core.Theming;

/// <summary>
/// The theming contract every other view consumes. Owns the selected-vs-resolved distinction,
/// persistence, and live switching (design-system-theming/phase.md, SPEC §3.7).
/// </summary>
public interface IThemeService
{
    /// <summary>The user's selection: Light, Dark, or System.</summary>
    AppTheme SelectedTheme { get; }

    /// <summary>
    /// The active resolved theme — always <see cref="AppTheme.Light"/> or
    /// <see cref="AppTheme.Dark"/>. When <see cref="SelectedTheme"/> is
    /// <see cref="AppTheme.System"/> this follows the OS setting.
    /// </summary>
    AppTheme CurrentTheme { get; }

    /// <summary>The resolved token set for <see cref="CurrentTheme"/>.</summary>
    ThemeTokens CurrentTokens { get; }

    /// <summary>Selects a theme, persists the choice, and applies it live.</summary>
    void SetTheme(AppTheme theme);

    /// <summary>Raised whenever <see cref="CurrentTheme"/> changes (selection or OS change).</summary>
    event EventHandler? ThemeChanged;
}

namespace MeticulousResearch.Core.Theming;

/// <summary>
/// Resolves the operating-system's current light/dark preference and notifies when it changes,
/// so <see cref="AppTheme.System"/> can be resolved and live-updated. The WPF app supplies a real
/// implementation; tests supply a fake.
/// </summary>
public interface ISystemThemeProvider
{
    /// <summary>
    /// The OS preference resolved to a concrete theme — always <see cref="AppTheme.Light"/> or
    /// <see cref="AppTheme.Dark"/>, never <see cref="AppTheme.System"/>.
    /// </summary>
    AppTheme GetSystemTheme();

    /// <summary>Raised when the OS light/dark preference changes.</summary>
    event EventHandler? SystemThemeChanged;
}

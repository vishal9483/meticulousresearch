using Microsoft.Win32;
using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.App.Theme;

/// <summary>
/// WPF <see cref="ISystemThemeProvider"/> backed by the Windows personalization setting. Reads
/// <c>AppsUseLightTheme</c> from the registry and raises <see cref="SystemThemeChanged"/> when the
/// user changes their OS theme, so a System selection live-updates (design-system-theming/phase.md).
/// </summary>
public sealed class WpfSystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>Subscribes to OS preference-change notifications.</summary>
    public WpfSystemThemeProvider() =>
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

    /// <inheritdoc />
    public AppTheme GetSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        var value = key?.GetValue(AppsUseLightThemeValue);
        // 1 (or missing) == light, 0 == dark.
        var appsUseLight = value is int i ? i != 0 : true;
        return appsUseLight ? AppTheme.Light : AppTheme.Dark;
    }

    /// <inheritdoc />
    public event EventHandler? SystemThemeChanged;

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose() =>
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}

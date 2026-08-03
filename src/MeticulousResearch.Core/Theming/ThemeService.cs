namespace MeticulousResearch.Core.Theming;

/// <summary>
/// The default <see cref="IThemeService"/>. Resolves <see cref="AppTheme.System"/> via the OS,
/// persists the selection, and raises <see cref="ThemeChanged"/> on selection or OS changes so
/// the shell can swap resource dictionaries live (design-system-theming/phase.md).
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly IThemeStore _store;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private AppTheme _selectedTheme;
    private AppTheme _currentTheme;

    /// <summary>
    /// Restores the persisted selection (defaulting to <see cref="AppTheme.System"/>) and resolves
    /// the active theme.
    /// </summary>
    public ThemeService(IThemeStore store, ISystemThemeProvider systemThemeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _systemThemeProvider = systemThemeProvider ?? throw new ArgumentNullException(nameof(systemThemeProvider));

        _selectedTheme = _store.Load() ?? AppTheme.System;
        _currentTheme = Resolve(_selectedTheme);
        _systemThemeProvider.SystemThemeChanged += OnSystemThemeChanged;
    }

    /// <inheritdoc />
    public AppTheme SelectedTheme => _selectedTheme;

    /// <inheritdoc />
    public AppTheme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public ThemeTokens CurrentTokens => DesignTokens.For(_currentTheme);

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <inheritdoc />
    public void SetTheme(AppTheme theme)
    {
        _selectedTheme = theme;
        _store.Save(theme);
        UpdateResolved();
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        // Only a System selection tracks the OS; an explicit Light/Dark ignores OS changes.
        if (_selectedTheme == AppTheme.System)
            UpdateResolved();
    }

    private void UpdateResolved()
    {
        var resolved = Resolve(_selectedTheme);
        if (resolved == _currentTheme)
            return;
        _currentTheme = resolved;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppTheme Resolve(AppTheme selected) =>
        selected == AppTheme.System ? _systemThemeProvider.GetSystemTheme() : selected;

    /// <summary>Unsubscribes from OS change notifications.</summary>
    public void Dispose() => _systemThemeProvider.SystemThemeChanged -= OnSystemThemeChanged;
}

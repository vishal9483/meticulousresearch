using System.IO;
using System.Windows;
using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.App.Theme;

/// <summary>
/// Applies the resolved theme to the running WPF application by swapping a single merged theme
/// dictionary (Light.xaml ↔ Dark.xaml) at the app level, so a live theme switch restyles every
/// window with no restart (design-system-theming/phase.md #3).
/// </summary>
public sealed class WpfThemeApplier : IDisposable
{
    private static readonly Uri LightSource =
        new("pack://application:,,,/MeticulousResearch.App;component/Theme/Light.xaml");
    private static readonly Uri DarkSource =
        new("pack://application:,,,/MeticulousResearch.App;component/Theme/Dark.xaml");

    private readonly IThemeService _themeService;
    private readonly Application _application;
    private ResourceDictionary? _activeThemeDictionary;

    /// <summary>Wires the applier to the theme service and applies the current theme immediately.</summary>
    public WpfThemeApplier(IThemeService themeService, Application application)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _themeService.ThemeChanged += OnThemeChanged;
        Apply(_themeService.CurrentTheme);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Marshal to the UI thread; OS-change events can arrive off-thread.
        _application.Dispatcher.Invoke(() => Apply(_themeService.CurrentTheme));
    }

    private void Apply(AppTheme resolvedTheme)
    {
        var source = resolvedTheme == AppTheme.Dark ? DarkSource : LightSource;
        var next = new ResourceDictionary { Source = source };

        var merged = _application.Resources.MergedDictionaries;
        if (_activeThemeDictionary is not null)
            merged.Remove(_activeThemeDictionary);

        // Insert the theme tokens first so control styles (added after) resolve their brushes.
        merged.Insert(0, next);
        _activeThemeDictionary = next;
    }

    /// <inheritdoc />
    public void Dispose() => _themeService.ThemeChanged -= OnThemeChanged;
}

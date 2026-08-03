using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MeticulousResearch.App.Theme;
using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.App;

/// <summary>
/// Interaction logic for App.xaml. Builds the generic host + DI container (SPEC §7.1) and shows
/// the shell window on startup.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private WpfThemeApplier? _themeApplier;

    /// <summary>Builds the host and registers app services.</summary>
    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddAppServices())
            .Build();
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply the persisted/resolved theme before the first window is shown, and keep applying
        // it live on selection or OS changes (design-system-theming/phase.md).
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        _themeApplier = new WpfThemeApplier(themeService, this);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _themeApplier?.Dispose();
        _host.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Flips between the Light and Dark palettes (an explicit selection). The full
    /// Light/Dark/System choice is surfaced later in Settings.
    /// </summary>
    public void ToggleTheme()
    {
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        var next = themeService.CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        themeService.SetTheme(next);
    }
}

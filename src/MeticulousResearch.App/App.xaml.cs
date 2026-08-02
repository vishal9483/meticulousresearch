using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeticulousResearch.App;

/// <summary>
/// Interaction logic for App.xaml. Builds the generic host + DI container (SPEC §7.1) and shows
/// the shell window on startup.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

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
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}

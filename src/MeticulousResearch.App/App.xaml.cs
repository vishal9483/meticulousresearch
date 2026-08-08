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

        // @ui harness only: seed a populated offline sample project so content journeys (resources,
        // artifacts, versions, diff, cost) have data without a key or network.
        if (System.Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") == "1")
        {
            var projects = _host.Services.GetRequiredService<MeticulousResearch.Core.Projects.IProjectService>();
            if (projects.List(includeArchived: true).Count == 0)
            {
                var sample = _host.Services.GetRequiredService<MeticulousResearch.Core.Onboarding.ISampleProjectFactory>().CreateSampleProject();

                // @ui only: add a captioned image resource (image-vision-caption, SPEC §3.2.1) so the
                // Resources preview has a thumbnail + cached caption to show. The @ui resource service
                // is registered with caption-on-add + a deterministic offline captioner.
                var resources = _host.Services.GetRequiredService<MeticulousResearch.Core.Resources.IResourceService>();
                var imagePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"sample-image-{System.Guid.NewGuid():N}.png");
                System.IO.File.WriteAllBytes(imagePath, MeticulousResearch.Core.Onboarding.SampleContent.ImageBytes);
                try { resources.AddImage(sample.Id, imagePath); }
                finally { try { System.IO.File.Delete(imagePath); } catch { /* best-effort */ } }
            }

            // A small context budget so the seeded resources genuinely exceed it, exercising the
            // composer's over-budget warning deterministically (context-budget, SPEC §3.2).
            _host.Services.GetRequiredService<MeticulousResearch.Core.Settings.ISettingsService>().ContextBudget = 100;
        }

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

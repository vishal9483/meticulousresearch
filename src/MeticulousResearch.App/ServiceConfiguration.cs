using Microsoft.Extensions.DependencyInjection;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App;

/// <summary>
/// Composition root for the WPF shell: registers the navigation service, the shell, and every
/// navigable view-model so no destination resolves to a placeholder (SPEC §1.3, §9.1(10)).
/// Later features add their view-models here alongside a DataTemplate.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>Registers the shell, navigation, and all navigable view-models.</summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Navigation service resolves view-models through the container. String navigation
        // parameters (e.g. a project id) are forwarded as constructor arguments so project-scoped
        // view-models are built already scoped to their project.
        services.AddSingleton<INavigationService>(sp => new NavigationService((type, parameters) =>
        {
            var ctorArgs = parameters
                .Where(p => p is string)
                .Cast<object>()
                .ToArray();
            return (ViewModelBase)ActivatorUtilities.CreateInstance(sp, type, ctorArgs);
        }));

        services.AddSingleton<ShellViewModel>();

        // Navigable destinations. Transient because project-scoped ones are built per navigation
        // with a project id; the home is cheap and stateless enough to be transient too.
        services.AddTransient<ProjectsHomeViewModel>();
        services.AddTransient<ProjectWorkspaceViewModel>();
        services.AddTransient<ConversationsViewModel>();
        services.AddTransient<ResourcesViewModel>();
        services.AddTransient<ArtifactsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProjectSettingsViewModel>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}

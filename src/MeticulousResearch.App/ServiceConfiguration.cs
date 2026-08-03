using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Theme;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Environment;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Url;
using MeticulousResearch.Core.Search;
using MeticulousResearch.Core.Security;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Theming;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.App;

/// <summary>
/// Composition root for the WPF shell: registers persistence, secure credentials, app settings,
/// theming, the project domain service, navigation, and every navigable view-model so no
/// destination resolves to a placeholder (SPEC 1.3, 9.1(10)).
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>Registers persistence, credentials, theming, project services, and all view-models.</summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Persistence + secure credentials + app settings (settings-secure-key/phase.md).
        var dataDirectory = DefaultDataDirectory();
        services.AddSingleton<IClock, SystemClock>();
        services.AddDataStore(dataDirectory);
        services.AddSingleton<IEnvironment, SystemEnvironment>();
        services.AddSingleton<ISecureKeyStore>(_ =>
            new DpapiSecureKeyStore(System.IO.Path.Combine(dataDirectory, "credentials.dat")));
        services.AddSingleton<ISettingsService>(sp => new SettingsService(sp.GetRequiredService<DataStore>()));
        services.AddSingleton<IApiCredentialProvider, ApiCredentialProvider>();
        services.AddSingleton<IDataDirectoryValidator, DataDirectoryValidator>();
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IKeyTester, KeyTester>();

        // Model catalog (model-selector/phase.md, SPEC §6.3): the config-driven tier/id/price catalog
        // consumed by ai-gateway (model id) and cost-tracking (prices). Loads a user override JSON at
        // a known path under the data directory when present, else the shipped default.
        services.AddSingleton<IModelCatalog>(_ =>
            ModelCatalogLoader.LoadFromFile(System.IO.Path.Combine(dataDirectory, "model-catalog.json")).Catalog);

        // Project domain service (projects-crud/phase.md): CRUD + dashboard aggregation.
        services.AddSingleton<IProjectService>(sp =>
            new ProjectService(sp.GetRequiredService<DataStore>(), sp.GetRequiredService<ISettingsService>()));

        // Resource domain service (text-paste-resource/phase.md): text paste add/preview + token
        // estimate hook. Siblings extend the estimator and add file/URL/image resource types.
        services.AddSingleton<ITokenEstimator, HeuristicTokenEstimator>();
        services.AddSingleton<IUrlFetcher>(sp => new HttpUrlFetcher(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IResourceService>(sp =>
            new ResourceService(
                sp.GetRequiredService<DataStore>(),
                sp.GetRequiredService<ITokenEstimator>(),
                sp.GetRequiredService<IUrlFetcher>()));

        // Full-text search over resource extracted text (full-text-search/phase.md): reads the
        // FTS5 index/triggers owned by data-store-migrations, project-scoped and relevance-ranked.
        services.AddSingleton<ISearchService>(sp => new SearchService(sp.GetRequiredService<DataStore>()));

        // AI gateway (ai-gateway/phase.md, SPEC §7.1–§7.3): the single generation contract behind
        // IChatService with a sidecar (primary) and direct-API (fallback) backend, selected in
        // settings. Downstream M2 features consume only IChatService / IArtifactService.
        services.AddSingleton<ChatRequestAssembler>();
        services.AddSingleton<IDirectApiTransport>(sp =>
            new HttpDirectApiTransport(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<ISidecarProcessFactory, NodeSidecarProcessFactory>();
        services.AddSingleton<SidecarSupervisor>(sp =>
            new SidecarSupervisor(sp.GetRequiredService<ISidecarProcessFactory>(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<DirectApiChatService>(sp => new DirectApiChatService(
            sp.GetRequiredService<IApiCredentialProvider>(),
            sp.GetRequiredService<ChatRequestAssembler>(),
            sp.GetRequiredService<IDirectApiTransport>()));
        services.AddSingleton<SidecarChatService>(sp => new SidecarChatService(
            sp.GetRequiredService<IApiCredentialProvider>(),
            sp.GetRequiredService<ChatRequestAssembler>(),
            sp.GetRequiredService<SidecarSupervisor>()));
        services.AddSingleton<IChatBackendFactory>(sp => new ChatBackendFactory(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<SidecarChatService>,
            sp.GetRequiredService<DirectApiChatService>));
        services.AddSingleton<IChatService>(sp => sp.GetRequiredService<IChatBackendFactory>().Resolve());
        services.AddSingleton<IArtifactService, NotImplementedArtifactService>();        // Pre-send context-budget estimate (context-budget/phase.md): enabled-resource scope +
        // overhead vs the selected model window (hard ceiling) and configured budget (soft), never
        // truncating silently.
        services.AddSingleton<IContextBudgetService>(sp =>
            new ContextBudgetService(
                sp.GetRequiredService<IResourceService>(),
                sp.GetRequiredService<ISettingsService>()));

        // Conversation domain service (conversations/phase.md, SPEC §3.3/§5/§7.3): project-scoped
        // grounded Q&A threads + message persistence, driving generation via IChatService.
        services.AddSingleton<IConversationService>(sp =>
            new ConversationService(
                sp.GetRequiredService<DataStore>(),
                sp.GetRequiredService<IChatService>(),
                sp.GetRequiredService<IProjectService>(),
                sp.GetRequiredService<IResourceService>(),
                sp.GetRequiredService<IClock>()));

        // Design system and theming (design-system-theming/phase.md).
        services.AddSingleton<ISystemThemeProvider, WpfSystemThemeProvider>();
        services.AddSingleton<IThemeStore>(_ => new JsonFileThemeStore(DefaultThemeSettingPath()));
        services.AddSingleton<IThemeService, ThemeService>();

        // Navigation service resolves view-models through the container. String navigation
        // parameters (e.g. a project id) are forwarded as constructor arguments.
        services.AddSingleton<INavigationService>(sp => new NavigationService((type, parameters) =>
        {
            var ctorArgs = parameters
                .Where(p => p is string)
                .Cast<object>()
                .ToArray();
            return (ViewModelBase)ActivatorUtilities.CreateInstance(sp, type, ctorArgs);
        }));

        services.AddSingleton<ShellViewModel>();

        // Navigable destinations.
        services.AddTransient<ProjectsHomeViewModel>();
        services.AddTransient<ProjectWorkspaceViewModel>();
        services.AddTransient<ConversationsViewModel>();
        services.AddTransient<ResourcesViewModel>();
        services.AddTransient<ArtifactsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProjectSettingsViewModel>();
        services.AddTransient<ThemeGalleryViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<MainWindow>();

        return services;
    }

    private static string DefaultDataDirectory()
    {
        return System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "MeticulousResearch");
    }

    private static string DefaultThemeSettingPath()
    {
        return System.IO.Path.Combine(DefaultDataDirectory(), "theme.json");
    }
}


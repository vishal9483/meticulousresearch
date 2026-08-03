using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.App.Theme;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
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
using MeticulousResearch.Core.Templates;
using MeticulousResearch.Core.Theming;
using MeticulousResearch.Core.Time;
using MeticulousResearch.Core.Turns;

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

        // Rate-limit & transient-error backoff (rate-limit-backoff/phase.md, SPEC §8): decorate the
        // resolved backend so every consumer (conversations, streaming, artifacts) retries 429 /
        // transient 5xx with exponential backoff + jitter, honors retry-after, and surfaces a
        // non-alarming "retrying…" state (RetryStatusViewModel) without losing work.
        services.AddSingleton<IJitterSource, RandomJitterSource>();
        services.AddSingleton<IRetryDelay, SystemRetryDelay>();
        services.AddSingleton<RetryStatusViewModel>();
        services.AddSingleton<IRetryObserver>(sp => sp.GetRequiredService<RetryStatusViewModel>());
        services.AddSingleton<IChatService>(sp => new RetryingChatService(
            sp.GetRequiredService<IChatBackendFactory>().Resolve(),
            new BackoffPolicy(TimeSpan.FromSeconds(1), maxAttempts: 5, sp.GetRequiredService<IJitterSource>()),
            sp.GetRequiredService<IRetryDelay>(),
            sp.GetRequiredService<IRetryObserver>()));
        services.AddSingleton<IArtifactService>(sp => new ArtifactService(
            sp.GetRequiredService<DataStore>(),
            sp.GetRequiredService<IChatService>(),
            sp.GetRequiredService<IClock>(),
            new CatalogTurnCostCalculator(sp.GetRequiredService<IModelCatalog>())));

        // Artifact diff engine (artifact-diff/phase.md, SPEC §3.4): a pure, read-only diff over the
        // version history owned by artifact-versioning. Consumed by the artifact editor's diff mode
        // and, later, by edit-with-claude to review a Claude edit before keeping it.
        services.AddSingleton<IArtifactDiffService, ArtifactDiffService>();

        // Deliverable-template catalog + service (deliverable-templates/phase.md, SPEC §3.4.1):
        // config-driven research templates (shipped default merged with a Settings override JSON)
        // that drive artifact generation through IArtifactService.Generate with grounding-first
        // prompting; also composes projects-crud creation for "new project from template".
        services.AddSingleton<ITemplateCatalog>(_ =>
            TemplateCatalogLoader.LoadFromFile(System.IO.Path.Combine(dataDirectory, "template-catalog.json")).Catalog);
        services.AddSingleton<ITemplateService>(sp => new TemplateService(
            sp.GetRequiredService<ITemplateCatalog>(),
            sp.GetRequiredService<IArtifactService>(),
            sp.GetRequiredService<IResourceService>(),
            sp.GetRequiredService<IModelCatalog>(),
            sp.GetRequiredService<IProjectService>()));
        services.AddTransient<TemplateGalleryViewModel>(sp =>
            new TemplateGalleryViewModel(sp.GetRequiredService<ITemplateCatalog>()));

        // Pre-send context-budget estimate (context-budget/phase.md): enabled-resource scope +
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

        // Streaming generation (streaming/phase.md, SPEC §3.3/§8): renders replies token-by-token,
        // adds stop/cancel, and persists interrupted turns (partial text marked interrupted) so
        // nothing is lost; an interrupted turn is resumable.
        services.AddSingleton<IStreamingConversationService>(sp =>
            new StreamingConversationService(
                sp.GetRequiredService<DataStore>(),
                sp.GetRequiredService<IChatService>(),
                sp.GetRequiredService<IProjectService>(),
                sp.GetRequiredService<IResourceService>(),
                sp.GetRequiredService<IClock>()));

        // Per-turn metadata, cost badge, and actions (turn-metadata-actions/phase.md, SPEC §3.3/§3.6):
        // retry (same/other model), edit-and-resend, promote-to-artifact (request/provenance; artifact
        // domain is M3), and delete, plus an inline cost badge priced through the ITurnCostCalculator
        // seam (authoritative engine is cost-tracking, M4).
        services.AddSingleton<ITurnCostCalculator>(sp =>
            new CatalogTurnCostCalculator(sp.GetRequiredService<IModelCatalog>()));
        services.AddSingleton<ITurnActionService>(sp =>
            new TurnActionService(
                sp.GetRequiredService<DataStore>(),
                sp.GetRequiredService<IConversationService>(),
                sp.GetRequiredService<IResourceService>()));
        services.AddSingleton<IClipboardService, WpfClipboardService>();

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


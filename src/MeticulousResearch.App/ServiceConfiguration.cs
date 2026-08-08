using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using MeticulousResearch.App.Commands;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.App.Theme;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Backoff;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Backup;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Environment;
using MeticulousResearch.Core.Export;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Onboarding;
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
using MeticulousResearch.Core.ViewStates;
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

        // Shared view-state error mapping (empty-loading-error-states/phase.md, SPEC §3.7): turns
        // known failures and unexpected exceptions into human-readable, actionable errors while the
        // raw detail is logged off-screen — never a raw stack trace in the UI.
        services.AddSingleton<IErrorLog, TraceErrorLog>();
        services.AddSingleton<IUserErrorMapper>(sp =>
            new UserErrorMapper(sp.GetRequiredService<IErrorLog>()));

        // Model catalog (model-selector/phase.md, SPEC §6.3): the config-driven tier/id/price catalog
        // consumed by ai-gateway (model id) and cost-tracking (prices). Loads a user override JSON at
        // a known path under the data directory when present, else the shipped default.
        services.AddSingleton<IModelCatalog>(_ =>
            ModelCatalogLoader.LoadFromFile(System.IO.Path.Combine(dataDirectory, "model-catalog.json")).Catalog);

        // Cost engine (cost-tracking/phase.md, SPEC §3.6): the authoritative cost computation for the
        // whole app. Prices are read from the model catalog; totals recompute from stored tokens so a
        // price change reprices history. Consumed by usage-csv-export (per-turn priced rows) and the
        // dashboard's consolidated cost panel.
        services.AddSingleton<ICostPriceSource>(sp =>
            new CatalogCostPriceSource(sp.GetRequiredService<IModelCatalog>()));
        services.AddSingleton<ICostService>(sp => new CostService(
            sp.GetRequiredService<DataStore>(),
            sp.GetRequiredService<ICostPriceSource>(),
            sp.GetRequiredService<IClock>()));

        // Usage CSV export (usage-csv-export/phase.md, SPEC §3.6, §9.1(7)): a deterministic, offline
        // serializer over the cost engine's per-turn priced rows — raw data, not a branded deliverable.
        services.AddSingleton<IUsageCsvExporter>(sp =>
            new UsageCsvExporter(sp.GetRequiredService<ICostService>()));

        // Project backup & restore (backup-restore/phase.md, SPEC §8, §9.1(9)): snapshots a single
        // project (DB subset + files + manifest) to a portable zip and restores it transactionally;
        // never includes vault secrets or another project's rows.
        services.AddSingleton<IProjectBackupService>(sp =>
            new ProjectBackupService(sp.GetRequiredService<DataStore>()));

        // Project domain service (projects-crud/phase.md): CRUD + dashboard aggregation.
        services.AddSingleton<IProjectService>(sp =>
            new ProjectService(sp.GetRequiredService<DataStore>(), sp.GetRequiredService<ISettingsService>()));

        // First-run onboarding (onboarding/phase.md, SPEC §3.8, §9.1(1)): the persisted completed
        // flag/step, the bundled offline sample-project builder, first-run hint state, the wizard
        // view-model, and the launch coordinator that shows the wizard on a clean install.
        services.AddSingleton<IOnboardingState>(sp => new OnboardingState(sp.GetRequiredService<DataStore>()));
        services.AddSingleton<ISampleProjectFactory>(sp => new SampleProjectFactory(
            sp.GetRequiredService<IProjectService>(),
            sp.GetRequiredService<IResourceService>(),
            sp.GetRequiredService<IArtifactService>()));
        services.AddSingleton<IFirstRunHints, FirstRunHints>();
        services.AddTransient<OnboardingViewModel>(sp => new OnboardingViewModel(
            sp.GetRequiredService<IOnboardingState>(),
            sp.GetRequiredService<ISecureKeyStore>(),
            sp.GetRequiredService<IApiCredentialProvider>(),
            sp.GetRequiredService<IKeyTester>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IDataDirectoryValidator>(),
            sp.GetRequiredService<ISampleProjectFactory>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IFirstRunHints>(),
            dataDirectory));
        services.AddSingleton<OnboardingCoordinator>(sp => new OnboardingCoordinator(
            sp.GetRequiredService<IOnboardingState>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<OnboardingViewModel>));

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

        // Edit-with-Claude (edit-with-claude/phase.md, SPEC §3.4, §9.1(5)): a follow-up instruction
        // that generates a new artifact version through the shared chat gateway, grounded in the
        // project's enabled resources and priced through the cost seam. Commits via AddVersion.
        services.AddSingleton<IEditWithClaudeService>(sp => new EditWithClaudeService(
            sp.GetRequiredService<IArtifactService>(),
            sp.GetRequiredService<IChatService>(),
            sp.GetRequiredService<IProjectService>(),
            sp.GetRequiredService<IResourceService>(),
            new CatalogTurnCostCalculator(sp.GetRequiredService<IModelCatalog>())));

        // Branded export (branded-export/phase.md, SPEC §3.4.2, §9.1(6)): deterministic, offline
        // DOCX/PDF rendering. Preview never writes to disk; only Save does.
        services.AddSingleton<IExportService>(sp => new ExportService(sp.GetRequiredService<IClock>()));

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

        // Command palette & keyboard shortcuts (command-palette-shortcuts/phase.md, SPEC §3.5):
        // the command registry (core commands + jump-to-project) and the palette view-model that
        // ranks a query and invokes the chosen command. Actions delegate to existing destinations
        // through the shared navigation service.
        services.AddSingleton<ICommandActions>(sp =>
            new ShellCommandActions(sp.GetRequiredService<INavigationService>()));
        services.AddSingleton<ICommandRegistry>(sp => new CommandRegistry(
            sp.GetRequiredService<IProjectService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<ICommandActions>()));
        services.AddTransient<CommandPaletteViewModel>();

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

        // About screen (about-screen/phase.md, SPEC §3.7): app identity + assembly-sourced version.
        services.AddSingleton<Core.AppInfo.IAppInfo>(_ =>
            new Core.AppInfo.AssemblyAppInfo(typeof(App).Assembly));

        // Update notice (update-notice/phase.md, SPEC §8): a thin HTTP adapter fetches the latest
        // advertised version; the comparison + dismissal memory live in Core.UpdateService. Failures
        // resolve silently to "no notice" and are logged off-screen (§7.5, §9.1(10)).
        services.AddSingleton<Core.Updates.ILatestVersionProvider>(sp =>
            new Services.HttpLatestVersionProvider(sp.GetRequiredService<HttpClient>(), updateSourceUri: null));
        services.AddSingleton<Core.Updates.IUpdateService>(sp => new Core.Updates.UpdateService(
            sp.GetRequiredService<Core.AppInfo.IAppInfo>(),
            sp.GetRequiredService<Core.Updates.ILatestVersionProvider>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IErrorLog>()));
        services.AddTransient<AboutViewModel>(sp => new AboutViewModel(
            sp.GetRequiredService<Core.AppInfo.IAppInfo>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<Core.Updates.IUpdateService>()));

        services.AddSingleton<MainWindow>();

        // @ui harness only: replace the generation backend with a deterministic offline fake so
        // conversation/streaming/turn journeys run without a key or network. Last registration wins,
        // and the conversation/streaming/artifact services resolve IChatService lazily.
        if (System.Environment.GetEnvironmentVariable("METICULOUS_UI_FAKE_AI") == "1")
        {
            // Wrap the fake in the same backoff decorator the real backend uses, wired to the shared
            // retry observer, so the @ui harness exercises the non-alarming "retrying…" indicator on
            // a scripted 429 (rate-limit-backoff, SPEC §8) — otherwise deterministic and offline.
            services.AddSingleton<IChatService>(sp => new Core.Ai.Backoff.RetryingChatService(
                new Services.FakeChatService(),
                new Core.Ai.Backoff.BackoffPolicy(TimeSpan.FromSeconds(1), maxAttempts: 5, sp.GetRequiredService<Core.Ai.Backoff.IJitterSource>()),
                sp.GetRequiredService<Core.Ai.Backoff.IRetryDelay>(),
                sp.GetRequiredService<Core.Ai.Backoff.IRetryObserver>()));
        }

        // @ui harness only: enable caption-on-add with a deterministic offline captioner so the
        // seeded image resource carries a cached caption (image-vision-caption, SPEC §3.2.1) without
        // a vision call, key, or network. Last registration wins.
        if (System.Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") == "1")
        {
            services.AddSingleton<IResourceService>(sp => new ResourceService(
                sp.GetRequiredService<DataStore>(),
                sp.GetRequiredService<ITokenEstimator>(),
                Core.Resources.Extraction.FileExtractionPipeline.CreateDefault(),
                sp.GetRequiredService<IUrlFetcher>(),
                new Services.SampleImageCaptioner(),
                new Core.Resources.Vision.ImageCaptionOptions { CaptionOnAdd = true }));
        }

        return services;
    }

    private static string DefaultDataDirectory()
    {
        // Allow an environment override so an isolated/clean data directory can be used (e.g. by the
        // FlaUI @ui harness). Never overrides secure credentials, which stay in the vault.
        var overrideDir = System.Environment.GetEnvironmentVariable("METICULOUS_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        return System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "MeticulousResearch");
    }

    private static string DefaultThemeSettingPath()
    {
        return System.IO.Path.Combine(DefaultDataDirectory(), "theme.json");
    }
}


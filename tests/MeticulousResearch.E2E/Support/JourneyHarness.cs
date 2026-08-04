using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Backup;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Export;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Reports;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Resources.Vision;
using MeticulousResearch.Core.Search;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Templates;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.E2E.Support;

/// <summary>
/// The shared, hermetic harness for the <c>@e2e @unit</c> journeys (E2E-TEST-SUITE.md §2). It wires
/// the <em>real</em> Core services — the same object graph the app composes — over a fresh temp
/// <see cref="DataStore"/> (WAL SQLite + on-disk project files), a scripted <see cref="FakeChatService"/>,
/// a <see cref="FakeEnvironment"/> for env-first credential resolution, a pinned price table, and a
/// deterministic <see cref="AdvancingClock"/>. Nothing touches <c>%LOCALAPPDATA%</c> or the network.
/// Each journey constructs one harness and disposes it (temp dir torn down) at the end.
/// </summary>
public sealed class JourneyHarness : IDisposable
{
    private readonly string _dataDir;
    private readonly List<string> _tempFiles = new();

    /// <summary>The deterministic, strictly-increasing clock backing every timestamp.</summary>
    public AdvancingClock Clock { get; }

    /// <summary>The scripted AI backend (token streams, usage, tool logs, error codes).</summary>
    public FakeChatService Chat { get; } = new();

    /// <summary>The fake process environment used for env-first key/base-url resolution.</summary>
    public FakeEnvironment Env { get; } = new();

    /// <summary>The DPAPI-shaped secure key store double (persisted key path).</summary>
    public FakeSecureKeyStore KeyStore { get; } = new();

    /// <summary>The loopback URL fetcher double (no real network) backing URL resources.</summary>
    public FakeUrlFetcher UrlFetcher { get; } = new();

    /// <summary>The real WAL SQLite + file store rooted at a disposable temp directory.</summary>
    public DataStore Store { get; }

    /// <summary>Pinned per-MTok prices so all cost math is stable regardless of catalog updates.</summary>
    public DictionaryCostPriceSource Prices { get; } = new();

    /// <summary>The config-driven model catalog (tiers, ids, vision flags, prices).</summary>
    public IModelCatalog Models { get; } = ModelCatalogLoader.Default;

    /// <summary>The config-driven deliverable-template catalog.</summary>
    public ITemplateCatalog TemplateCatalog { get; } = TemplateCatalogLoader.Default;

    public SettingsService Settings { get; }
    public ProjectService Projects { get; }
    public ResourceService Resources { get; }
    public ConversationService Conversations { get; }
    public MessageAttachmentStore AttachmentStore { get; }
    public StreamingConversationService Streaming { get; }
    public ArtifactService Artifacts { get; }
    public ArtifactDiffService Diff { get; } = new();
    public SearchService Search { get; }
    public CostService Cost { get; }
    public ContextBudgetService Budget { get; }
    public TurnActionService TurnActions { get; }
    public ProjectBackupService Backup { get; }
    public UsageCsvExporter UsageCsv { get; }
    public ExportService Export { get; }
    public EditWithClaudeService EditWithClaude { get; }
    public ReportCompositionService Reports { get; }
    public TemplateService Templates { get; }
    public SampleProjectFactory Sample { get; }
    public ApiCredentialProvider Credentials { get; }

    /// <summary>Builds the whole real Core object graph over a fresh temp store.</summary>
    public JourneyHarness()
    {
        Clock = new AdvancingClock();
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-e2e", Guid.NewGuid().ToString("N"));

        Store = new DataStore(Clock, _dataDir);
        Store.Initialize();

        // Pinned test price catalog (USD per million tokens; explicit cache rates).
        Prices.SetRates("claude-opus-5", new CostRates(5m, 25m, 0.5m, 6.25m));
        Prices.SetRates("claude-sonnet-5", new CostRates(3m, 15m, 0.3m, 3.75m));
        Prices.SetRates("claude-haiku-4-5", new CostRates(1m, 5m, 0.1m, 1.25m));

        Settings = new SettingsService(Store);
        Projects = new ProjectService(Store, Settings);
        Resources = new ResourceService(
            Store,
            new HeuristicTokenEstimator(),
            FileExtractionPipeline.CreateDefault(),
            UrlFetcher,
            new DeterministicImageCaptioner(),
            ImageCaptionOptions.Default);
        AttachmentStore = new MessageAttachmentStore(Store.FileStore);
        Conversations = new ConversationService(
            Store, Chat, Projects, Resources, Clock, new ConversationGroundingAssembler(), AttachmentStore);
        Streaming = new StreamingConversationService(Store, Chat, Projects, Resources, Clock);
        var costCalculator = new CatalogTurnCostCalculator(Models);
        Artifacts = new ArtifactService(Store, Chat, Clock, costCalculator);
        Search = new SearchService(Store);
        Cost = new CostService(Store, Prices, Clock);
        Budget = new ContextBudgetService(Resources, Settings);
        TurnActions = new TurnActionService(Store, Conversations, Resources);
        Backup = new ProjectBackupService(Store);
        UsageCsv = new UsageCsvExporter(Cost);
        Export = new ExportService(Clock);
        EditWithClaude = new EditWithClaudeService(Artifacts, Chat, Projects, Resources, costCalculator);
        Reports = new ReportCompositionService(Artifacts);
        Templates = new TemplateService(TemplateCatalog, Artifacts, Resources, Models, Projects);
        Sample = new SampleProjectFactory(Projects, Resources, Artifacts);
        Credentials = new ApiCredentialProvider(Env, KeyStore, Settings);
    }

    /// <summary>The absolute data directory this harness owns (temp; torn down on dispose).</summary>
    public string DataDirectory => _dataDir;

    /// <summary>Builds the enabled-resource grounding scope for a project (id, title, extracted text).</summary>
    public IReadOnlyList<ChatResource> EnabledScope(string projectId) =>
        Resources.ListEnabled(projectId)
            .Select(r => new ChatResource(r.Id, r.Title, Resources.GetExtractedText(r.Id)))
            .ToList();

    /// <summary>Writes a valid 2x2 PNG to a temp file and returns its path (cleaned up on dispose).</summary>
    public string NewImageFile()
    {
        var path = Path.Combine(_dataDir, $"img-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, SamplePng.Bytes);
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>Allocates a temp destination path with the given extension (cleaned up on dispose).</summary>
    public string NewTempPath(string extension)
    {
        var path = Path.Combine(_dataDir, $"out-{Guid.NewGuid():N}.{extension}");
        _tempFiles.Add(path);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Store.ClearConnectionPool();
        Store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }
}

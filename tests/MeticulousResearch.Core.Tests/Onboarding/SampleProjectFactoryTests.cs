using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Onboarding;

/// <summary>
/// Faithful <c>@unit</c> translation of the sample-project scenarios from
/// docs/features/onboarding/tests.md (SPEC §3.8(4)). Exercises the real
/// <see cref="SampleProjectFactory"/> over real project/resource/artifact services and a temp
/// SQLite store, with a <see cref="FakeChatService"/> that must never be called (proving the sample
/// is built from bundled content, offline, with no key).
/// </summary>
public sealed class SampleProjectFactoryTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly ArtifactService _artifacts;
    private readonly FakeChatService _chat = new();
    private readonly SampleProjectFactory _factory;

    public SampleProjectFactoryTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-onboarding-sample", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _artifacts = new ArtifactService(_store, _chat, _clock);
        _factory = new SampleProjectFactory(_projects, _resources, _artifacts);
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
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

    // Scenario: Opting in creates a populated sample research project
    //   When I choose to create the sample project
    //   Then a sample project exists
    //   And it contains a couple of resources
    //   And it contains an example "Market Research Report" artifact
    [Fact]
    public void Opting_in_creates_a_populated_sample_project()
    {
        var project = _factory.CreateSampleProject();

        // a sample project exists
        Assert.NotNull(_projects.Get(project.Id));

        // it contains a couple of resources
        var resources = _resources.List(project.Id);
        Assert.Equal(2, resources.Count);

        // it contains an example "Market Research Report" artifact
        var artifacts = _artifacts.List(project.Id);
        var report = Assert.Single(artifacts, a => a.Title == "Market Research Report");
        Assert.Equal(ArtifactTypes.Doc, report.Type);
    }

    // Scenario: The sample project is skipped without error when no key is configured
    //   Given I skipped the API key step
    //   When I opt into the sample project
    //   Then the sample project is created from bundled content without a network call
    [Fact]
    public void Sample_project_is_created_from_bundled_content_without_a_network_call()
    {
        // No API key / secure store is involved and the chat service must never be invoked.
        var project = _factory.CreateSampleProject();

        Assert.NotNull(_projects.Get(project.Id));
        Assert.Equal(2, _resources.List(project.Id).Count);
        Assert.Single(_artifacts.List(project.Id), a => a.Title == "Market Research Report");

        // Bundled content only — no generation / no network call.
        Assert.Equal(0, _chat.AskCount);
    }
}

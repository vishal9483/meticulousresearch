using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the Artifacts section view-model (docs/features/artifact-creation/tests.md,
/// SPEC §3.4). Window-free: they wire a real <see cref="ArtifactService"/> over a temp store so the
/// designed empty state, the artifact list, and the "New artifact" entry point are proven without a
/// WPF window. These back the @ui "designed empty state" and "New artifact opens the editor" scenarios.
/// </summary>
public sealed class ArtifactsViewModelTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ArtifactService _service;
    private readonly string _projectId;

    public ArtifactsViewModelTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-artifacts-vm-tests", Guid.NewGuid().ToString("N"));
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        _store = new DataStore(clock, _dataDir);
        _store.Initialize();
        _service = new ArtifactService(_store, new FakeChatService(), clock);
        _projectId = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "EV Batteries 2026",
            Archived = false,
            CreatedAt = clock.UtcNow.ToString("o"),
            UpdatedAt = clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
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

    // A project with no artifacts renders the designed empty state.
    [Fact]
    public void A_project_with_no_artifacts_is_empty()
    {
        var vm = new ArtifactsViewModel(_projectId, _service);

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasArtifacts);
        Assert.Empty(vm.Artifacts);
        Assert.False(string.IsNullOrWhiteSpace(ArtifactsViewModel.EmptyStateMessage));
    }

    // The list reflects the project's artifacts.
    [Fact]
    public void The_list_shows_the_projects_artifacts()
    {
        _service.Create(_projectId, "doc", "Existing draft");

        var vm = new ArtifactsViewModel(_projectId, _service);

        Assert.False(vm.IsEmpty);
        Assert.True(vm.HasArtifacts);
        Assert.Contains(vm.Artifacts, a => a.Title == "Existing draft" && a.Type == "doc");
    }

    // "New artifact" creates a real artifact and selects it (a real editor destination).
    [Fact]
    public void New_artifact_creates_and_selects_a_real_artifact()
    {
        var vm = new ArtifactsViewModel(_projectId, _service);

        vm.NewArtifactCommand.Execute(null);

        Assert.False(vm.IsEmpty);
        Assert.NotNull(vm.SelectedArtifact);
        Assert.Contains(vm.Artifacts, a => a.Id == vm.SelectedArtifact!.Id);
        Assert.NotNull(_service.Get(vm.SelectedArtifact!.Id));
    }
}

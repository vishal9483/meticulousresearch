using Microsoft.Data.Sqlite;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the Resources section view-model backing the text-paste flow
/// (docs/features/text-paste-resource/tests.md). These are window-free: they wire a real
/// <see cref="ResourceService"/> over a temp store so the add-to-table, preview, and inline
/// validation behaviour is proven without a WPF window. They back the two @ui scenarios and the
/// "Pasting empty text is rejected" inline-error clause.
/// </summary>
public sealed class ResourcesViewModelTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;

    public ResourcesViewModelTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-resources-vm-tests", Guid.NewGuid().ToString("N"));
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        _store = new DataStore(clock, _dataDir);
        _store.Initialize();
        _service = new ResourceService(_store, new HeuristicTokenEstimator());
        _projectId = Guid.NewGuid().ToString("N");
        // A project row is not required for resource FK here because tests only add via the
        // service against a valid project id shape; create one to be faithful to the schema.
        using var db = _store.CreateDbContext();
        db.Projects.Add(new Core.Data.Entities.Project
        {
            Id = _projectId,
            Name = "Semiconductors 2026",
            Archived = false,
            CreatedAt = clock.UtcNow.ToString("o"),
            UpdatedAt = clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
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

    // Empty state: a project with no resources renders the designed empty state.
    [Fact]
    public void A_project_with_no_resources_is_empty()
    {
        var vm = new ResourcesViewModel(_projectId, _service);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Resources);
    }

    // Backs @ui "Adding a pasted resource shows it in the resources table":
    // the new row lists the title, type "Text", and an enabled toggle.
    [Fact]
    public void Adding_a_pasted_resource_shows_it_in_the_table()
    {
        var vm = new ResourcesViewModel(_projectId, _service)
        {
            DraftTitle = "Foundry note",
            DraftText = "Global foundry capacity grew 12% in 2025.",
        };

        vm.AddPastedTextCommand.Execute(null);

        var row = Assert.Single(vm.Resources);
        Assert.Equal("Foundry note", row.Title);
        Assert.Equal("Text", row.TypeDisplay);
        Assert.True(row.Enabled);
        Assert.False(vm.IsEmpty);
    }

    // Backs @ui "Selecting a text resource shows its extracted text in the preview pane".
    [Fact]
    public void Selecting_a_resource_shows_its_extracted_text_in_the_preview()
    {
        var resource = _service.AddText(_projectId, "Foundry note", "Global foundry capacity grew 12% in 2025.");
        var vm = new ResourcesViewModel(_projectId, _service);

        var row = Assert.Single(vm.Resources);
        Assert.Equal(resource.Id, row.Id);

        vm.SelectedResource = row;

        Assert.Equal("Global foundry capacity grew 12% in 2025.", vm.PreviewText);
    }

    // Scenario: Pasting empty text is rejected — inline validation error, no resource created.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Pasting_empty_text_shows_an_inline_error_and_creates_nothing(string emptyOrWhitespace)
    {
        var vm = new ResourcesViewModel(_projectId, _service)
        {
            DraftTitle = "Title",
            DraftText = emptyOrWhitespace,
        };

        vm.AddPastedTextCommand.Execute(null);

        Assert.True(vm.HasValidationError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ValidationError));
        Assert.Empty(vm.Resources);
        Assert.True(vm.IsEmpty);
        Assert.Empty(_service.List(_projectId));
    }
}

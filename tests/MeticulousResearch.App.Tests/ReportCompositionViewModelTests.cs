using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Reports;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the report composition view-model (docs/features/report-composition/tests.md,
/// SPEC §3.4.1). Window-free: a real <see cref="ReportCompositionService"/> over a real
/// <see cref="ArtifactService"/> and temp store proves the ordered section list, the add-section
/// entry point, and the designed empty-state guidance without a WPF window. These back the @ui
/// "ordered list" / "add section" scenarios and the empty-composition guidance clause.
/// </summary>
public sealed class ReportCompositionViewModelTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ArtifactService _artifacts;
    private readonly ReportCompositionService _compositions;
    private readonly string _projectId;
    private readonly Artifact _execSummary;
    private readonly Artifact _marketSizing;
    private readonly Artifact _forecastTable;

    public ReportCompositionViewModelTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-report-composition-vm-tests", Guid.NewGuid().ToString("N"));
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        _store = new DataStore(clock, _dataDir);
        _store.Initialize();
        _artifacts = new ArtifactService(_store, new FakeChatService(), clock);
        _compositions = new ReportCompositionService(_artifacts);

        _projectId = Guid.NewGuid().ToString("N");
        using (var db = _store.CreateDbContext())
        {
            db.Projects.Add(new Project
            {
                Id = _projectId,
                Name = "Grid Storage 2026",
                Archived = false,
                CreatedAt = clock.UtcNow.ToString("o"),
                UpdatedAt = clock.UtcNow.ToString("o"),
            });
            db.SaveChanges();
        }

        _execSummary = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Executive Summary", "EXEC BODY", null, ArtifactProvenance.User());
        _marketSizing = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Sizing", "SIZING BODY", null, ArtifactProvenance.User());
        _forecastTable = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Table, "Forecast Table", "Year,GWh\n2026,10", null, ArtifactProvenance.User());
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

    private ReportCompositionViewModel CreateViewModel(string compositionId) =>
        new(compositionId, _projectId, _compositions, _artifacts);

    // Empty composition: the view prompts the analyst to add sections (empty-composition scenario).
    [Fact]
    public void An_empty_composition_prompts_the_analyst_to_add_sections()
    {
        var comp = _compositions.CreateComposition(_projectId, "Full Report");

        var vm = CreateViewModel(comp.Id);

        Assert.False(vm.HasSections);
        Assert.True(vm.IsEmpty);
        Assert.Equal(ReportCompositionViewModel.EmptyStatePrompt, vm.EmptyStateMessage);
        Assert.False(string.IsNullOrWhiteSpace(vm.EmptyStateMessage));
    }

    // The view lists sections in order (backs the @ui ordered-list scenario).
    [Fact]
    public void The_view_lists_sections_in_order()
    {
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _execSummary.Id);
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        var vm = CreateViewModel(comp.Id);

        Assert.True(vm.HasSections);
        Assert.Equal(
            new[] { "Executive Summary", "Market Sizing", "Forecast Table" },
            vm.Sections.Select(s => s.Title).ToArray());
    }

    // Adding an artifact as a section is offered and works (backs the @ui add-section scenario).
    [Fact]
    public void Adding_an_artifact_as_a_section_appends_it()
    {
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        var vm = CreateViewModel(comp.Id);

        // The view offers existing project artifacts to add (excluding the composition itself).
        Assert.Contains(vm.AvailableArtifacts, a => a.Id == _execSummary.Id);
        Assert.DoesNotContain(vm.AvailableArtifacts, a => a.Id == comp.Id);

        vm.AddSectionCommand.Execute(_execSummary.Id);

        Assert.True(vm.HasSections);
        Assert.Equal("Executive Summary", vm.Sections.Single().Title);
    }

    // Drag-to-reorder moves a section within the ordered list.
    [Fact]
    public void Reordering_moves_a_section_within_the_list()
    {
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _execSummary.Id);
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        var vm = CreateViewModel(comp.Id);
        var forecastId = vm.Sections.Single(s => s.Title == "Forecast Table").SectionId;

        vm.MoveUpCommand.Execute(forecastId);

        Assert.Equal(
            new[] { "Executive Summary", "Forecast Table", "Market Sizing" },
            vm.Sections.Select(s => s.Title).ToArray());
    }
}

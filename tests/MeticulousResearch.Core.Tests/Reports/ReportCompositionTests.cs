using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Reports;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Reports;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> scenario in
/// docs/features/report-composition/tests.md (SPEC §3.4.1, §9.1(6)). None carry an excluded
/// <c>Category</c> trait, so they run in the headless gate over a real
/// <see cref="ReportCompositionService"/> layered on a real <see cref="ArtifactService"/> and temp
/// SQLite store. An <see cref="AdvancingClock"/> gives deterministic timestamps (TESTING-STRATEGY §4).
///
/// Background: a project "Grid Storage 2026" with artifacts Executive Summary (doc),
/// Market Sizing (doc), Forecast Table (table), Competitive Landscape (table).
/// </summary>
public sealed class ReportCompositionTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ArtifactService _artifacts;
    private readonly ReportCompositionService _compositions;
    private readonly string _projectId;
    private readonly Artifact _execSummary;
    private readonly Artifact _marketSizing;
    private readonly Artifact _forecastTable;
    private readonly Artifact _competitiveLandscape;

    public ReportCompositionTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-report-composition-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _artifacts = new ArtifactService(
            _store, new FakeChatService(), _clock, new CatalogTurnCostCalculator(ModelCatalogLoader.Default));
        _compositions = new ReportCompositionService(_artifacts);

        _projectId = _projects.Create("Grid Storage 2026").Id;
        _execSummary = CreateArtifact(ArtifactTypes.Doc, "Executive Summary", "EXEC BODY");
        _marketSizing = CreateArtifact(ArtifactTypes.Doc, "Market Sizing", "SIZING BODY");
        _forecastTable = CreateArtifact(ArtifactTypes.Table, "Forecast Table", "Year,GWh\n2026,10\n2027,20");
        _competitiveLandscape = CreateArtifact(ArtifactTypes.Table, "Competitive Landscape", "Vendor,Share\nA,40");
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

    private Artifact CreateArtifact(string type, string title, string content) =>
        _artifacts.CreateFromContent(_projectId, type, title, content, contentFormat: null, ArtifactProvenance.User());

    private string CompositionContent(string compositionId)
    {
        var artifact = _artifacts.Get(compositionId);
        Assert.NotNull(artifact);
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking().Single(v => v.Id == artifact!.CurrentVersionId).Content;
    }

    // ----- Creating and ordering a composition -----

    // Scenario: Creating a report composition produces a document artifact
    [Fact]
    public void Creating_a_report_composition_produces_a_document_artifact()
    {
        // When I create a report composition titled "Grid Storage 2026 — Full Report"
        var comp = _compositions.CreateComposition(_projectId, "Grid Storage 2026 — Full Report");

        // Then a document artifact "Grid Storage 2026 — Full Report" exists
        var stored = _artifacts.Get(comp.Id);
        Assert.NotNull(stored);
        Assert.Equal("Grid Storage 2026 — Full Report", stored!.Title);
        Assert.Equal(ArtifactTypes.Doc, stored.Type);

        // And it is marked as a report composition
        Assert.True(_compositions.IsComposition(comp.Id));
        Assert.False(_compositions.IsComposition(_marketSizing.Id));
    }

    // Scenario: Adding artifacts to a composition records them as ordered section references
    [Fact]
    public void Adding_artifacts_records_them_as_ordered_section_references()
    {
        // Given a report composition
        var comp = _compositions.CreateComposition(_projectId, "Full Report");

        // When I add "Executive Summary", then "Market Sizing", then "Forecast Table"
        _compositions.AddSection(comp.Id, _execSummary.Id);
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        // Then the composition references those three artifacts in that order
        var sections = _compositions.GetSections(comp.Id);
        Assert.Equal(
            new[] { _execSummary.Id, _marketSizing.Id, _forecastTable.Id },
            sections.Select(s => s.ArtifactId).ToArray());

        // And it references them (does not copy their content)
        var content = CompositionContent(comp.Id);
        Assert.Contains(_execSummary.Id, content);
        Assert.DoesNotContain("EXEC BODY", content);
        Assert.DoesNotContain("SIZING BODY", content);
    }

    // Scenario: Reordering sections changes the composition order
    [Fact]
    public void Reordering_sections_changes_the_composition_order()
    {
        // Given a composition ordered Executive Summary, Market Sizing, Forecast Table
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        var exec = _compositions.AddSection(comp.Id, _execSummary.Id);
        var market = _compositions.AddSection(comp.Id, _marketSizing.Id);
        var forecast = _compositions.AddSection(comp.Id, _forecastTable.Id);

        // When I move "Forecast Table" above "Market Sizing"
        _compositions.ReorderSections(comp.Id, new[] { exec.SectionId, forecast.SectionId, market.SectionId });

        // Then the order is Executive Summary, Forecast Table, Market Sizing
        var sections = _compositions.GetSections(comp.Id);
        Assert.Equal(
            new[] { _execSummary.Id, _forecastTable.Id, _marketSizing.Id },
            sections.Select(s => s.ArtifactId).ToArray());
    }

    // Scenario: Removing a section drops it from the composition but not the project
    [Fact]
    public void Removing_a_section_drops_it_from_the_composition_but_not_the_project()
    {
        // Given a composition containing "Market Sizing"
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        var market = _compositions.AddSection(comp.Id, _marketSizing.Id);

        // When I remove "Market Sizing" from the composition
        _compositions.RemoveSection(comp.Id, market.SectionId);

        // Then the composition no longer references "Market Sizing"
        Assert.DoesNotContain(
            _marketSizing.Id, _compositions.GetSections(comp.Id).Select(s => s.ArtifactId));

        // And the "Market Sizing" artifact still exists in the project
        Assert.NotNull(_artifacts.Get(_marketSizing.Id));
        Assert.Contains(_artifacts.List(_projectId), a => a.Id == _marketSizing.Id);
    }

    // ----- References track their source artifacts -----

    // Scenario: A section reflects its source artifact's current version
    [Fact]
    public void A_section_reflects_its_source_artifacts_current_version()
    {
        // Given a composition referencing "Market Sizing" at its current version
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        Assert.Contains("SIZING BODY", _compositions.Render(comp.Id).Content);

        // When "Market Sizing" gets a new current version via Edit with Claude
        _artifacts.SetContent(_marketSizing.Id, "SIZING BODY v2");

        // Then the composition's rendered section reflects the new current version
        var rendered = _compositions.Render(comp.Id).Content;
        Assert.Contains("SIZING BODY v2", rendered);
        Assert.DoesNotContain("SIZING BODY\n", rendered + "\n"); // old body no longer present verbatim
    }

    // Scenario: A composition can pin a section to a specific version
    [Fact]
    public void A_composition_can_pin_a_section_to_a_specific_version()
    {
        // Given a composition referencing "Forecast Table"
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        var section = _compositions.AddSection(comp.Id, _forecastTable.Id);

        // Forecast Table advances: v2, then v3.
        _artifacts.SetContent(_forecastTable.Id, "Year,GWh\n2026,11"); // version 2
        var v2Id = _artifacts.GetHistory(_forecastTable.Id).Single(v => v.VersionNo == 2).Id;

        // When I pin that section to version 2
        _compositions.PinSectionVersion(comp.Id, section.SectionId, v2Id);

        // and Forecast Table advances to version 3
        _artifacts.SetContent(_forecastTable.Id, "Year,GWh\n2026,99"); // version 3

        // Then the section renders version 2 even after "Forecast Table" advances to version 3
        var rendered = _compositions.Render(comp.Id).Content;
        Assert.Contains("11", rendered);
        Assert.DoesNotContain("99", rendered);
    }

    // ----- Rendering the compiled document -----

    // Scenario: The compiled document concatenates sections in order
    [Fact]
    public void The_compiled_document_concatenates_sections_in_order()
    {
        // Given a composition ordered Executive Summary, Market Sizing, Forecast Table
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _execSummary.Id);
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        // When I render the composition
        var content = _compositions.Render(comp.Id).Content;

        // Then the compiled content contains each section's content in that order
        var execAt = content.IndexOf("EXEC BODY", StringComparison.Ordinal);
        var sizingAt = content.IndexOf("SIZING BODY", StringComparison.Ordinal);
        var forecastAt = content.IndexOf("2026", StringComparison.Ordinal);
        Assert.True(execAt >= 0 && sizingAt >= 0 && forecastAt >= 0);
        Assert.True(execAt < sizingAt, "Executive Summary should precede Market Sizing.");
        Assert.True(sizingAt < forecastAt, "Market Sizing should precede Forecast Table.");
    }

    // Scenario: Each section carries its artifact title as a heading
    [Fact]
    public void Each_section_carries_its_artifact_title_as_a_heading()
    {
        // Given a composition with a "Market Sizing" section
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _marketSizing.Id);

        // When I render the composition
        var report = _compositions.Render(comp.Id);

        // Then the compiled document includes a "Market Sizing" section heading
        Assert.Contains("## Market Sizing", report.Content);
        Assert.Contains(report.Sections, s => s.Title == "Market Sizing");
    }

    // Scenario: A table section renders as a table within the document
    [Fact]
    public void A_table_section_renders_as_a_table_within_the_document()
    {
        // Given a composition containing the "Forecast Table" table artifact
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        // When I render the composition
        var content = _compositions.Render(comp.Id).Content;

        // Then the table's rows appear as a table in the compiled document
        Assert.Contains("| Year | GWh |", content);
        Assert.Contains("| --- | --- |", content);
        Assert.Contains("| 2026 | 10 |", content);
        Assert.Contains("| 2027 | 20 |", content);
    }

    // ----- Validation & edge cases -----

    // Scenario: A section referencing a deleted artifact is flagged
    [Fact]
    public void A_section_referencing_a_deleted_artifact_is_flagged()
    {
        // Given a composition referencing "Competitive Landscape"
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _competitiveLandscape.Id);

        // When the "Competitive Landscape" artifact is deleted
        _artifacts.DeleteArtifact(_competitiveLandscape.Id);

        // Then the composition flags a broken section reference
        var report = _compositions.Render(comp.Id);
        Assert.True(report.HasBrokenReferences);
        Assert.Contains(report.Sections, s => s.IsBroken);

        // And rendering skips it with a visible placeholder note
        var broken = report.Sections.Single(s => s.IsBroken);
        Assert.Contains("Missing section", broken.Body);
        Assert.Contains("Missing section", report.Content);
        Assert.DoesNotContain("Vendor,Share", report.Content);
    }

    // Scenario: An empty composition renders an empty document with guidance
    [Fact]
    public void An_empty_composition_renders_an_empty_document_with_guidance()
    {
        // Given a report composition with no sections
        var comp = _compositions.CreateComposition(_projectId, "Full Report");

        // When I render it
        var report = _compositions.Render(comp.Id);

        // Then the compiled document is empty
        Assert.True(report.IsEmpty);
        Assert.Equal("", report.Content);

        // (the composition view prompts the analyst to add sections — asserted in the view-model tests)
    }

    // ----- Export hand-off (§3.4.1 / §9.1(6)) -----

    // Scenario: A composition exposes its ordered sections for export
    [Fact]
    public void A_composition_exposes_its_ordered_sections_for_export()
    {
        // Given a composition ordered Executive Summary, Market Sizing, Forecast Table
        var comp = _compositions.CreateComposition(_projectId, "Full Report");
        _compositions.AddSection(comp.Id, _execSummary.Id);
        _compositions.AddSection(comp.Id, _marketSizing.Id);
        _compositions.AddSection(comp.Id, _forecastTable.Id);

        // When export requests the composition's content
        var report = _compositions.Render(comp.Id);

        // Then it receives the sections in composition order as a single document
        Assert.Equal(
            new[] { "Executive Summary", "Market Sizing", "Forecast Table" },
            report.Sections.Select(s => s.Title).ToArray());
        Assert.False(string.IsNullOrEmpty(report.Content));
    }
}

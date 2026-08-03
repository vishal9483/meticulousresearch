using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Artifacts;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> scenario in
/// docs/features/artifact-diff/tests.md (SPEC §3.4 diff between any two versions). None carry an
/// excluded <c>Category</c> trait, so they run in the headless gate. Versions are produced through
/// the real <see cref="ArtifactService"/>/<see cref="IArtifactService.AddVersion"/> history contract
/// owned by <c>artifact-versioning</c> and read back via <see cref="IArtifactService.GetHistory"/>;
/// the diff itself is computed by the pure <see cref="ArtifactDiffService"/>.
///
/// Background: an artifact "Executive Summary" with versions:
///   | 1 | The market is $2B. |
///   | 2 | The market is $3B. |
///   | 3 | The market is $3B and growing. |
/// </summary>
public sealed class ArtifactDiffTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly FakeChatService _chat = new();
    private readonly ArtifactService _artifacts;
    private readonly ArtifactDiffService _diff = new();
    private readonly string _projectId;
    private readonly Artifact _artifact;

    public ArtifactDiffTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-artifact-diff-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _artifacts = new ArtifactService(
            _store, _chat, _clock, new CatalogTurnCostCalculator(ModelCatalogLoader.Default));

        _projectId = _projects.Create("EV Batteries 2026").Id;

        // Background version history.
        _artifact = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Executive Summary", "The market is $2B.",
            contentFormat: null, ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "The market is $3B.", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "The market is $3B and growing.", ArtifactProvenance.User());
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

    private ArtifactVersion Version(string artifactId, long versionNo) =>
        _artifacts.GetHistory(artifactId).Single(v => v.VersionNo == versionNo);

    private static IReadOnlyList<string> Removed(ArtifactDiff diff) =>
        diff.RemovedSegments.Select(s => s.Text).ToList();

    private static IReadOnlyList<string> Added(ArtifactDiff diff) =>
        diff.AddedSegments.Select(s => s.Text).ToList();

    // ----- Computing a diff -----

    // Scenario: Diffing two versions reports the changed lines
    //   When I diff version 1 against version 2
    //   Then the diff marks "The market is $2B." as removed
    //   And marks "The market is $3B." as added
    [Fact]
    public void Diffing_two_versions_reports_the_changed_lines()
    {
        var diff = _diff.Diff(Version(_artifact.Id, 1), Version(_artifact.Id, 2));

        Assert.Contains("The market is $2B.", Removed(diff));
        Assert.Contains("The market is $3B.", Added(diff));
    }

    // Scenario: Diffing identical content reports no changes
    //   Given two versions with identical content
    //   When I diff them
    //   Then the diff reports no differences
    [Fact]
    public void Diffing_identical_content_reports_no_changes()
    {
        var identical = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Identical", "The market is $2B.",
            contentFormat: null, ArtifactProvenance.User());
        _artifacts.AddVersion(identical.Id, "The market is $2B.", ArtifactProvenance.User());

        var diff = _diff.Diff(Version(identical.Id, 1), Version(identical.Id, 2));

        Assert.False(diff.HasChanges);
        Assert.Empty(Removed(diff));
        Assert.Empty(Added(diff));
    }

    // Scenario: Diffing additive-only changes marks only additions
    //   When I diff version 2 against version 3
    //   Then "and growing" is marked as added
    //   And nothing is marked as removed
    [Fact]
    public void Diffing_additive_only_changes_marks_only_additions()
    {
        var diff = _diff.Diff(Version(_artifact.Id, 2), Version(_artifact.Id, 3));

        Assert.Contains(Added(diff), text => text.Contains("and growing", StringComparison.Ordinal));
        Assert.Empty(Removed(diff));
    }

    // ----- Any two versions (not just adjacent) -----

    // Scenario: Non-adjacent versions can be compared
    //   When I diff version 1 against version 3
    //   Then the diff reflects all changes from version 1 to version 3
    [Fact]
    public void Non_adjacent_versions_can_be_compared()
    {
        var diff = _diff.Diff(Version(_artifact.Id, 1), Version(_artifact.Id, 3));

        Assert.True(diff.HasChanges);
        Assert.Contains("The market is $2B.", Removed(diff));
        Assert.Contains("The market is $3B and growing.", Added(diff));
    }

    // Scenario: Diff direction is respected (old → new)
    //   When I select version 3 as the base and version 1 as the compare
    //   Then "and growing" is marked as removed
    [Fact]
    public void Diff_direction_is_respected_old_to_new()
    {
        var diff = _diff.Diff(Version(_artifact.Id, 3), Version(_artifact.Id, 1));

        Assert.Contains(Removed(diff), text => text.Contains("and growing", StringComparison.Ordinal));
    }

    // ----- Format-aware diffing -----

    // Scenario: A table artifact diffs by rows/cells
    //   Given a "table" artifact whose version 2 adds a row and edits a cell
    //   When I diff version 1 against version 2
    //   Then the added row and the changed cell are reported
    [Fact]
    public void A_table_artifact_diffs_by_rows_cells()
    {
        var table = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Table, "Quarterly Sales",
            "Name,Q1\nAlpha,10\nBeta,20", contentFormat: null, ArtifactProvenance.User());
        // Version 2 edits Alpha's Q1 cell (10 → 15) and adds a new row (Gamma,30).
        _artifacts.AddVersion(table.Id, "Name,Q1\nAlpha,15\nBeta,20\nGamma,30", ArtifactProvenance.User());

        var diff = _diff.Diff(Version(table.Id, 1), Version(table.Id, 2));

        Assert.Equal("table", diff.Format);
        Assert.NotNull(diff.Table);
        Assert.True(diff.HasChanges);

        // The added row (Gamma,30) is reported.
        Assert.Contains(diff.Table!.AddedRows, row => row.Cells.Contains("Gamma") && row.Cells.Contains("30"));

        // The changed cell (Alpha's Q1: 10 → 15) is reported.
        Assert.Contains(diff.Table.ChangedCells, cell => cell.BaseValue == "10" && cell.CompareValue == "15");
    }

    // Scenario: A diagram artifact diffs its Mermaid source as text
    //   Given a "diagram" artifact whose version 2 changes one node label
    //   When I diff version 1 against version 2
    //   Then the changed source line is reported
    [Fact]
    public void A_diagram_artifact_diffs_its_mermaid_source_as_text()
    {
        var diagram = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Diagram, "Flow",
            "graph TD\nA[Start] --> B[Process]\nB --> C[End]", contentFormat: null, ArtifactProvenance.User());
        // Version 2 changes one node label: B[Process] → B[Review].
        _artifacts.AddVersion(diagram.Id, "graph TD\nA[Start] --> B[Review]\nB --> C[End]", ArtifactProvenance.User());

        var diff = _diff.Diff(Version(diagram.Id, 1), Version(diagram.Id, 2));

        Assert.Equal("text", diff.Format);
        Assert.True(diff.HasChanges);

        // The changed source line is reported (old label removed, new label added).
        Assert.Contains(Removed(diff), text => text.Contains("B[Process]", StringComparison.Ordinal));
        Assert.Contains(Added(diff), text => text.Contains("B[Review]", StringComparison.Ordinal));
    }
}

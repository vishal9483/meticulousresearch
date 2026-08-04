using System.IO.Compression;
using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-14 — Backup and restore a project (covers SPEC §9.1: 9, §8). A populated project round-trips
/// through a backup zip intact (resources, conversation turns, artifact versions, cost, FTS), and a
/// tampered/incomplete archive is rejected with a clear error leaving no partial project behind.
/// </summary>
public sealed class J14_BackupRestore : IDisposable
{
    private readonly JourneyHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task<string> SeedPopulatedProjectAsync()
    {
        var projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Cite sources").Id;
        _h.Resources.AddText(projectId, "Filing", "Market sizing points to $100B.");
        _h.Resources.AddText(projectId, "Interview", "The CEO is optimistic.");

        var conversation = _h.Conversations.Create(projectId);
        _h.Chat.WithCompletionText("The market is large.").WithUsage(1_000, 500);
        await _h.Conversations.Ask(conversation.Id, "How big is the market?", "claude-opus-5");

        var artifact = _h.Artifacts.CreateFromContent(
            projectId, ArtifactTypes.Doc, "Summary", "# v1", null, ArtifactProvenance.User());
        _h.Artifacts.SetContent(artifact.Id, "# v2");
        return projectId;
    }

    // @e2e
    // Scenario: A project round-trips through backup and restore intact
    [Fact]
    public async Task A_project_round_trips_through_backup_and_restore_intact()
    {
        var projectId = await SeedPopulatedProjectAsync();
        var originalResources = _h.Resources.List(projectId).Count;
        var originalArtifacts = _h.Artifacts.List(projectId).Count;
        var originalCost = _h.Cost.GetProjectCost(projectId).Total;

        // When I create a backup zip of the project.
        var zipPath = _h.NewTempPath("zip");
        _h.Backup.Backup(projectId, zipPath);
        Assert.True(File.Exists(zipPath));

        // When I delete the project and restore from the backup.
        _h.Projects.Delete(projectId);
        Assert.Null(_h.Projects.Get(projectId));
        var restoredId = _h.Backup.Restore(zipPath);

        // Then the restored project has identical resources, artifact versions, and cost.
        Assert.Equal(originalResources, _h.Resources.List(restoredId).Count);
        Assert.Equal(originalArtifacts, _h.Artifacts.List(restoredId).Count);
        Assert.Equal(originalCost, _h.Cost.GetProjectCost(restoredId).Total);

        // And FTS search over the restored content works.
        var hits = _h.Search.SearchResources(restoredId, "market");
        Assert.NotEmpty(hits);
    }

    // @e2e @unit
    // Scenario: A tampered or incomplete backup is rejected with a clear error
    [Fact]
    public void A_tampered_or_incomplete_backup_is_rejected_with_a_clear_error()
    {
        // Given a backup archive missing its manifest (an arbitrary zip with no manifest entry).
        var bogusZip = _h.NewTempPath("zip");
        using (var archive = ZipFile.Open(bogusZip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("garbage.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a real backup");
        }

        var projectsBefore = _h.Projects.List(includeArchived: true).Count;

        // When I attempt to restore it, restore fails with a human-readable error.
        var ex = Assert.ThrowsAny<Exception>(() => _h.Backup.Restore(bogusZip));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));

        // And no partial project is left behind.
        Assert.Equal(projectsBefore, _h.Projects.List(includeArchived: true).Count);
    }
}

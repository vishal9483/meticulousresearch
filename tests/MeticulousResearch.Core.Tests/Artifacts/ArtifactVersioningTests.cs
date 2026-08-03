using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Artifacts;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> scenario in
/// docs/features/artifact-versioning/tests.md (SPEC §3.4 versioning/management, §5 ArtifactVersion).
/// None carry an excluded <c>Category</c> trait, so they run in the headless gate over a real
/// <see cref="ArtifactService"/> and temp SQLite store. An <see cref="AdvancingClock"/> gives
/// deterministic, strictly-increasing timestamps and AI-generating edits are served by a scripted
/// <see cref="FakeChatService"/> (TESTING-STRATEGY §4).
///
/// Background: an artifact "Market Sizing" with version 1.
/// </summary>
public sealed class ArtifactVersioningTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ArtifactService _artifacts;
    private readonly string _projectId;
    private readonly Artifact _artifact;

    public ArtifactVersioningTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-artifact-versioning-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _artifacts = new ArtifactService(
            _store, _chat, _clock, new CatalogTurnCostCalculator(ModelCatalogLoader.Default));

        _projectId = _projects.Create("EV Batteries 2026").Id;

        // Background: an artifact "Market Sizing" with version 1.
        _artifact = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Sizing", "# v1", contentFormat: null, ArtifactProvenance.User());
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

    private ArtifactVersion CurrentVersion(string artifactId)
    {
        var artifact = _artifacts.Get(artifactId);
        Assert.NotNull(artifact);
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking().Single(v => v.Id == artifact!.CurrentVersionId);
    }

    private ArtifactVersion Version(string artifactId, long versionNo)
    {
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking().Single(v => v.ArtifactId == artifactId && v.VersionNo == versionNo);
    }

    // ----- New version on every change (immutability) -----

    // Scenario: A manual edit creates a new version and leaves the prior one unchanged
    [Fact]
    public void A_manual_edit_creates_a_new_version_and_leaves_the_prior_one_unchanged()
    {
        // Given the current version 1 content is "# v1"
        Assert.Equal("# v1", Version(_artifact.Id, 1).Content);

        // When I edit the content to "# v2" and save
        _artifacts.SetContent(_artifact.Id, "# v2");

        // Then a version 2 exists with content "# v2"
        var v2 = Version(_artifact.Id, 2);
        Assert.Equal("# v2", v2.Content);

        // And version 1's content is still "# v1"
        Assert.Equal("# v1", Version(_artifact.Id, 1).Content);

        // And version 2's created_by is "user"
        Assert.Equal("user", v2.CreatedBy);
    }

    // Scenario: A regeneration creates a new version
    [Fact]
    public async Task A_regeneration_creates_a_new_version()
    {
        // Given a FakeChatService that emits updated content
        _chat.WithCompletionText("# regenerated").WithUsage(10, 5);

        // When I regenerate the artifact
        await _artifacts.Regenerate(_artifact.Id, new GenerateArtifactRequest
        {
            Type = ArtifactTypes.Doc,
            Title = "Market Sizing",
            Prompt = "Regenerate the market sizing",
            Model = "claude-opus-5",
        });

        // Then a version 2 exists whose created_by is "claude"
        var v2 = Version(_artifact.Id, 2);
        Assert.Equal("claude", v2.CreatedBy);
        Assert.Equal("# regenerated", v2.Content);

        // And version 1 is unchanged
        Assert.Equal("# v1", Version(_artifact.Id, 1).Content);
    }

    // Scenario: A saved version cannot be mutated in place
    [Fact]
    public void A_saved_version_cannot_be_mutated_in_place()
    {
        // Given version 1 exists
        var v1 = Version(_artifact.Id, 1);

        // When I attempt to overwrite version 1's content directly
        // Then the operation is rejected
        Assert.Throws<NotSupportedException>(() => _artifacts.OverwriteVersionContent(v1.Id, "# hacked"));

        // And any change must go through creating a new version
        Assert.Equal("# v1", Version(_artifact.Id, 1).Content);
        _artifacts.AddVersion(_artifact.Id, "# next", ArtifactProvenance.User());
        Assert.Equal("# next", Version(_artifact.Id, 2).Content);
    }

    // ----- Ordered history -----

    // Scenario: Version numbers increase monotonically
    [Fact]
    public void Version_numbers_increase_monotonically()
    {
        // When I create three successive versions
        // (version 1 is the background; add two more so three exist in creation order)
        _artifacts.AddVersion(_artifact.Id, "b", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "c", ArtifactProvenance.User());

        // Then their version_no values are 1, 2, 3 in creation order
        using var db = _store.CreateDbContext();
        var versions = db.ArtifactVersions.AsNoTracking()
            .Where(v => v.ArtifactId == _artifact.Id)
            .OrderBy(v => v.CreatedAt)
            .ToList();
        Assert.Equal(new long[] { 1, 2, 3 }, versions.Select(v => v.VersionNo).ToArray());
    }

    // Scenario: History is ordered newest-to-oldest for display
    [Fact]
    public void History_is_ordered_newest_to_oldest_for_display()
    {
        // Given versions 1, 2, and 3
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3",
            ArtifactProvenance.Claude("claude-opus-5", "p", Array.Empty<string>(), 1, 1));

        // When I view the version history
        var history = _artifacts.GetHistory(_artifact.Id);

        // Then it lists version 3, then 2, then 1
        Assert.Equal(new long[] { 3, 2, 1 }, history.Select(v => v.VersionNo).ToArray());

        // And each entry shows its created_at, model, and created_by
        foreach (var v in history)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.CreatedAt));
            Assert.False(string.IsNullOrWhiteSpace(v.CreatedBy));
        }
        // version 3 was generated, so it carries a model; the display reads whatever is recorded.
        Assert.Equal("claude-opus-5", history[0].Model);
    }

    // ----- Version metadata (§5) -----

    // Scenario: A generated version records full provenance
    [Fact]
    public async Task A_generated_version_records_full_provenance()
    {
        // Given a FakeChatService returning tokens_in 900 and tokens_out 600
        _chat.WithCompletionText("# generated").WithUsage(900, 600);

        var resA = _resources.AddText(_projectId, "A", "alpha");
        var resB = _resources.AddText(_projectId, "B", "beta");

        // When I regenerate with model "claude-opus-5" and 2 in-scope resources
        var version = await _artifacts.Regenerate(_artifact.Id, new GenerateArtifactRequest
        {
            Type = ArtifactTypes.Doc,
            Title = "Market Sizing",
            Prompt = "Regenerate with sources",
            Model = "claude-opus-5",
            Resources = new[]
            {
                new ChatResource(resA.Id, resA.Title, "alpha"),
                new ChatResource(resB.Id, resB.Title, "beta"),
            },
        });

        // Then the new version records model "claude-opus-5", the prompt, the 2 resource ids, a
        // timestamp, tokens_in 900, tokens_out 600, and a cost_usd
        Assert.Equal("claude-opus-5", version.Model);
        Assert.Equal("Regenerate with sources", version.Prompt);
        Assert.False(string.IsNullOrWhiteSpace(version.CreatedAt));
        Assert.Equal(900, version.TokensIn);
        Assert.Equal(600, version.TokensOut);
        Assert.NotNull(version.CostUsd);
        Assert.True(version.CostUsd > 0);

        var scope = System.Text.Json.JsonSerializer.Deserialize<string[]>(version.ResourceScopeJson!);
        Assert.NotNull(scope);
        Assert.Equal(new[] { resA.Id, resB.Id }, scope);
    }

    // Scenario: A manual-edit version records zero usage
    [Fact]
    public void A_manual_edit_version_records_zero_usage()
    {
        // When I make a manual edit
        var version = _artifacts.SetContent(_artifact.Id, "# edited");

        // Then the new version's tokens_in, tokens_out, and cost_usd are 0
        Assert.Equal(0, version.TokensIn);
        Assert.Equal(0, version.TokensOut);
        Assert.True(version.CostUsd is null or 0);

        // And its model and prompt are null
        Assert.Null(version.Model);
        Assert.Null(version.Prompt);
    }

    // ----- Set current version -----

    // Scenario: Setting an older version as current changes what the editor shows
    [Fact]
    public void Setting_an_older_version_as_current_changes_what_the_editor_shows()
    {
        // Given versions 1, 2, and 3 with version 3 current
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var v1 = Version(_artifact.Id, 1);
        Assert.Equal(Version(_artifact.Id, 3).Id, _artifacts.Get(_artifact.Id)!.CurrentVersionId);

        // When I set version 1 as current
        _artifacts.SetCurrentVersion(_artifact.Id, v1.Id);

        // Then the artifact's current_version_id points at version 1
        Assert.Equal(v1.Id, _artifacts.Get(_artifact.Id)!.CurrentVersionId);

        // And the editor shows version 1's content
        Assert.Equal("# v1", CurrentVersion(_artifact.Id).Content);

        // And versions 2 and 3 still exist in history
        var history = _artifacts.GetHistory(_artifact.Id);
        Assert.Contains(history, v => v.VersionNo == 2);
        Assert.Contains(history, v => v.VersionNo == 3);
    }

    // Scenario: Setting current does not create a new version
    [Fact]
    public void Setting_current_does_not_create_a_new_version()
    {
        // Given 3 versions
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var v1 = Version(_artifact.Id, 1);

        // When I set version 1 as current
        _artifacts.SetCurrentVersion(_artifact.Id, v1.Id);

        // Then there are still 3 versions
        Assert.Equal(3, _artifacts.GetHistory(_artifact.Id).Count);
    }

    // ----- Revert -----

    // Scenario: Reverting to a version creates a new version copying its content
    [Fact]
    public void Reverting_to_a_version_creates_a_new_version_copying_its_content()
    {
        // Given versions 1 ("# v1"), 2 ("# v2"), and 3 ("# v3") with version 3 current
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var v1 = Version(_artifact.Id, 1);

        // When I revert to version 1
        var v4 = _artifacts.RevertTo(_artifact.Id, v1.Id);

        // Then a new version 4 exists with content "# v1"
        Assert.Equal(4, v4.VersionNo);
        Assert.Equal("# v1", Version(_artifact.Id, 4).Content);

        // And version 4 is current
        Assert.Equal(v4.Id, _artifacts.Get(_artifact.Id)!.CurrentVersionId);

        // And versions 1–3 are unchanged
        Assert.Equal("# v1", Version(_artifact.Id, 1).Content);
        Assert.Equal("# v2", Version(_artifact.Id, 2).Content);
        Assert.Equal("# v3", Version(_artifact.Id, 3).Content);
    }

    // Scenario: A reverted version records that it came from a revert
    [Fact]
    public void A_reverted_version_records_that_it_came_from_a_revert()
    {
        var v1 = Version(_artifact.Id, 1);

        // When I revert to version 1
        var reverted = _artifacts.RevertTo(_artifact.Id, v1.Id);

        // Then the new version's created_by is "user"
        Assert.Equal("user", reverted.CreatedBy);
    }

    // ----- Duplicate -----

    // Scenario: Duplicating an artifact copies its full version history
    [Fact]
    public void Duplicating_an_artifact_copies_its_full_version_history()
    {
        // Given an artifact with 3 versions
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var sourceCurrentContent = CurrentVersion(_artifact.Id).Content;

        // When I duplicate it as "Market Sizing (copy)"
        var copy = _artifacts.DuplicateArtifact(_artifact.Id, "Market Sizing (copy)");

        // Then a new artifact "Market Sizing (copy)" exists with 3 versions
        Assert.NotEqual(_artifact.Id, copy.Id);
        Assert.Equal("Market Sizing (copy)", copy.Title);
        Assert.Equal(3, _artifacts.GetHistory(copy.Id).Count);

        // And its current version matches the source's current version content
        Assert.Equal(sourceCurrentContent, CurrentVersion(copy.Id).Content);

        // And the original artifact is unchanged
        Assert.Equal(3, _artifacts.GetHistory(_artifact.Id).Count);
        Assert.Equal("Market Sizing", _artifacts.Get(_artifact.Id)!.Title);
    }

    // Scenario: A duplicated artifact is independent of the original
    [Fact]
    public void A_duplicated_artifact_is_independent_of_the_original()
    {
        // Given a duplicated artifact
        var copy = _artifacts.DuplicateArtifact(_artifact.Id, "Market Sizing (copy)");

        // When I edit the duplicate
        _artifacts.SetContent(copy.Id, "# copy edit");

        // Then the original artifact's versions are unaffected
        Assert.Single(_artifacts.GetHistory(_artifact.Id));
        Assert.Equal("# v1", CurrentVersion(_artifact.Id).Content);
        Assert.Equal(2, _artifacts.GetHistory(copy.Id).Count);
    }

    // ----- Delete -----

    // Scenario: Deleting an artifact removes it and all its versions
    [Fact]
    public void Deleting_an_artifact_removes_it_and_all_its_versions()
    {
        // Given an artifact with 3 versions
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());

        // When I delete it and confirm
        _artifacts.DeleteArtifact(_artifact.Id);

        // Then the artifact no longer exists
        Assert.Null(_artifacts.Get(_artifact.Id));

        // And none of its versions remain
        using var db = _store.CreateDbContext();
        Assert.Empty(db.ArtifactVersions.AsNoTracking().Where(v => v.ArtifactId == _artifact.Id).ToList());
    }

    // Scenario: Deleting a single non-current version keeps the rest
    [Fact]
    public void Deleting_a_single_non_current_version_keeps_the_rest()
    {
        // Given versions 1, 2, and 3 with version 3 current
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var v1 = Version(_artifact.Id, 1);
        var v3Id = Version(_artifact.Id, 3).Id;

        // When I delete version 1
        _artifacts.DeleteVersion(_artifact.Id, v1.Id);

        // Then versions 2 and 3 remain
        var history = _artifacts.GetHistory(_artifact.Id);
        Assert.Equal(new long[] { 3, 2 }, history.Select(v => v.VersionNo).ToArray());

        // And version 3 is still current
        Assert.Equal(v3Id, _artifacts.Get(_artifact.Id)!.CurrentVersionId);
    }

    // Scenario: The current version cannot be deleted directly
    [Fact]
    public void The_current_version_cannot_be_deleted_directly()
    {
        // Given version 3 is current
        _artifacts.AddVersion(_artifact.Id, "# v2", ArtifactProvenance.User());
        _artifacts.AddVersion(_artifact.Id, "# v3", ArtifactProvenance.User());
        var v3Id = _artifacts.Get(_artifact.Id)!.CurrentVersionId!;

        // When I try to delete version 3
        // Then the operation is rejected with a message to set another version current first
        var ex = Assert.Throws<InvalidOperationException>(() => _artifacts.DeleteVersion(_artifact.Id, v3Id));
        Assert.Contains("current", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And it still exists (nothing was deleted)
        Assert.Equal(3, _artifacts.GetHistory(_artifact.Id).Count);
    }

    // ----- Promote-to-resource -----

    // Scenario: Promoting an artifact to a resource creates an artifact_ref resource
    [Fact]
    public void Promoting_an_artifact_to_a_resource_creates_an_artifact_ref_resource()
    {
        // Given an artifact "Forecast Table" whose current version has content
        var forecast = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Table, "Forecast Table", "year,units\n2026,100",
            contentFormat: null, ArtifactProvenance.User());

        // When I promote it to a resource in the same project
        var resource = _artifacts.PromoteToResource(forecast.Id, _projectId);

        // Then a resource of type "artifact_ref" referencing that artifact exists
        Assert.Equal("artifact_ref", resource.Type);
        Assert.Equal(forecast.Id, resource.SourceUri);
        var loaded = _resources.Get(resource.Id);
        Assert.NotNull(loaded);
        Assert.Equal("artifact_ref", loaded!.Type);

        // And its extracted text is the artifact's current version content
        Assert.Equal("year,units\n2026,100", resource.ExtractedText);
        Assert.Equal("year,units\n2026,100", _resources.GetExtractedText(resource.Id));
    }

    // Scenario: A promoted resource can be enabled for grounding
    [Fact]
    public void A_promoted_resource_can_be_enabled_for_grounding()
    {
        // Given an artifact promoted to a resource
        var resource = _artifacts.PromoteToResource(_artifact.Id, _projectId);

        // When I enable that resource
        _resources.SetEnabled(resource.Id, true);

        // Then it is available as in-scope context for conversations and generations
        var enabled = _resources.ListEnabled(_projectId);
        Assert.Contains(enabled, r => r.Id == resource.Id);
    }
}

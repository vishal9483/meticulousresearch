using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-24 — Provenance &amp; "nothing is lost" invariants (cross-cutting; SPEC §1.3 principles 4 &amp; 5).
/// Every generated output records its provenance (model, in-scope resources, prompt, timestamp) and
/// the provenance chain survives promotion; edits, regenerations, and reverts never destroy or mutate
/// a prior version.
/// </summary>
public sealed class J24_ProvenanceNothingLost : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J24_ProvenanceNothingLost()
    {
        _projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Cite sources").Id;
        _h.Resources.AddText(_projectId, "Filing", "EV demand grows.");
    }

    public void Dispose() => _h.Dispose();

    // @e2e @unit
    // Scenario: Every generated output records its provenance
    [Fact]
    public async Task Every_generated_output_records_its_provenance()
    {
        var scope = _h.EnabledScope(_projectId);

        // A conversation turn records model, in-scope resources, and timestamp.
        var conversation = _h.Conversations.Create(_projectId);
        _h.Chat.WithCompletionText("Answer").WithUsage(100, 50);
        var turn = await _h.Conversations.Ask(conversation.Id, "Summarize", "claude-opus-5", scope);
        Assert.Equal("claude-opus-5", turn.Model);
        Assert.NotNull(turn.ResourceScopeJson);
        Assert.False(string.IsNullOrEmpty(turn.CreatedAt));

        // An artifact version generated from it records model, prompt, in-scope resources, and timestamp.
        _h.Chat.WithCompletionText("# Report").WithUsage(200, 80);
        var artifact = await _h.Artifacts.Generate(_projectId, new GenerateArtifactRequest
        {
            Type = ArtifactTypes.Doc,
            Title = "Report",
            Prompt = "Write the report",
            Model = "claude-opus-5",
            Resources = scope,
        });
        var version = _h.Artifacts.GetHistory(artifact.Id).Single();
        Assert.Equal("claude-opus-5", version.Model);
        Assert.False(string.IsNullOrEmpty(version.Prompt));
        Assert.NotNull(version.ResourceScopeJson);
        Assert.False(string.IsNullOrEmpty(version.CreatedAt));

        // Promoting an artifact to a resource preserves that provenance chain (a resource is created).
        var promoted = _h.Artifacts.PromoteToResource(artifact.Id, _projectId);
        Assert.NotNull(_h.Resources.Get(promoted.Id));
    }

    // @e2e @unit
    // Scenario: Edits and regenerations never destroy prior state
    [Fact]
    public async Task Edits_and_regenerations_never_destroy_prior_state()
    {
        // Given an artifact with several versions.
        var artifact = _h.Artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Sizing", "# v1", null, ArtifactProvenance.User());
        var v1Id = _h.Artifacts.GetHistory(artifact.Id).Single().Id;
        const string v1Content = "# v1";

        // When I edit (manual), regenerate (Claude), and revert.
        _h.Artifacts.SetContent(artifact.Id, "# v2 manual");
        _h.Chat.WithCompletionText("# v3 claude").WithUsage(120, 40);
        await _h.EditWithClaude.EditWithClaude(artifact.Id, "improve", "claude-opus-5");
        _h.Artifacts.RevertTo(artifact.Id, v1Id);

        // Then all prior versions remain retrievable in order (a contiguous version sequence).
        var history = _h.Artifacts.GetHistory(artifact.Id);
        Assert.True(history.Count >= 3);
        var versionNumbers = history.Select(v => v.VersionNo).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range(1, history.Count).Select(n => (long)n).ToList(), versionNumbers);

        // And no prior version's content is mutated (v1 is byte-for-byte intact).
        Assert.Equal(v1Content, history.Single(v => v.Id == v1Id).Content);
        Assert.Contains(history, v => v.Content == "# v2 manual");
        Assert.Contains(history, v => v.Content == "# v3 claude");
    }
}

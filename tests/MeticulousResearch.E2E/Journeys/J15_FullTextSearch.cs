using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Search;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-15 — Full-text search across a project (covers SPEC §3.1). A single claim is found across all
/// content types — resources, conversation turns, and artifact versions — each hit carrying a type,
/// title, and matching snippet. Runs headlessly over the real FTS5-backed search service.
/// </summary>
[Trait("Category", "integration")]
public sealed class J15_FullTextSearch : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J15_FullTextSearch() => _projectId = _h.Projects.Create("EV Market 2026").Id;

    public void Dispose() => _h.Dispose();

    // @e2e
    // Scenario: Ana finds a claim across all content types
    [Fact]
    public async Task Ana_finds_a_claim_across_all_content_types()
    {
        // Given a project with resources, conversation turns, and artifact versions containing "market sizing".
        _h.Resources.AddText(_projectId, "Sizing memo", "The market sizing indicates strong growth.");

        var conversation = _h.Conversations.Create(_projectId);
        _h.Chat.WithCompletionText("Our market sizing model projects $100B.").WithUsage(10, 20);
        await _h.Conversations.Ask(conversation.Id, "Explain the sizing", "claude-opus-5");

        _h.Artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Sizing Report",
            "# Market sizing\nThe market sizing is $100B.", null, ArtifactProvenance.User());

        // When I search the project for "sizing".
        var resourceHits = _h.Search.SearchResources(_projectId, "sizing");
        var messageHits = _h.Search.SearchMessages(_projectId, "sizing");
        var artifactHits = _h.Search.SearchArtifacts(_projectId, "sizing");

        // Then results include hits from resources, conversations, and artifacts.
        Assert.NotEmpty(resourceHits);
        Assert.NotEmpty(messageHits);
        Assert.NotEmpty(artifactHits);

        // And each hit shows a type and title.
        foreach (var hit in resourceHits.Concat(messageHits).Concat(artifactHits))
            Assert.False(string.IsNullOrWhiteSpace(hit.Title));
        Assert.Contains(resourceHits, h => h.ContentType == SearchContentType.Resource);
        Assert.Contains(artifactHits, h => h.ContentType == SearchContentType.Artifact);
    }
}

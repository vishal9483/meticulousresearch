using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Search;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Search;

/// <summary>
/// Faithful xUnit translation of docs/features/full-text-search/tests.md. These scenarios are
/// tagged <c>@unit @integration</c> (search over the real SQLite FTS5 index) or <c>@unit</c>; none
/// carry an excluded Category trait, so they run in the headless gate against a temp database
/// (TESTING-STRATEGY §4). The <c>@integration</c> secondary tag is recorded as a Trait.
///
/// Background: project "Semiconductors 2026" is open with three resources:
///   | Foundry note | Global foundry capacity grew 12% in 2025.       |
///   | Wafer note   | Wafer starts rose sharply across leading nodes. |
///   | Pricing memo | ASP declined in mature nodes during 2025.       |
/// </summary>
public sealed class SearchServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ResourceService _resources;
    private readonly SearchService _search;
    private readonly string _projectId;

    public SearchServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-fts-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _search = new SearchService(_store);

        _resources.AddText(_projectId, "Foundry note", "Global foundry capacity grew 12% in 2025.");
        _resources.AddText(_projectId, "Wafer note", "Wafer starts rose sharply across leading nodes.");
        _resources.AddText(_projectId, "Pricing memo", "ASP declined in mature nodes during 2025.");
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

    private List<string> Titles(IEnumerable<SearchHit> hits) => hits.Select(h => h.Title).ToList();

    // Scenario: A keyword search returns matching resources
    [Fact]
    [Trait("Category", "integration")]
    public void A_keyword_search_returns_matching_resources()
    {
        // When I search the project for "foundry"
        var titles = Titles(_search.SearchResources(_projectId, "foundry"));

        // Then the results include "Foundry note"
        Assert.Contains("Foundry note", titles);

        // And they exclude "Wafer note" and "Pricing memo"
        Assert.DoesNotContain("Wafer note", titles);
        Assert.DoesNotContain("Pricing memo", titles);
    }

    // Scenario: Search matches across the extracted text, not just the title
    [Fact]
    [Trait("Category", "integration")]
    public void Search_matches_across_the_extracted_text_not_just_the_title()
    {
        // When I search the project for "wafer"
        var titles = Titles(_search.SearchResources(_projectId, "wafer"));

        // Then the results include "Wafer note"
        Assert.Contains("Wafer note", titles);
    }

    // Scenario Outline: Search ranks and filters by relevance
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("2025", "Foundry note, Pricing memo")]
    [InlineData("nodes", "Wafer note, Pricing memo")]
    [InlineData("tin", "")]
    public void Search_ranks_and_filters_by_relevance(string query, string results)
    {
        // When I search the project for "<query>"
        var titles = Titles(_search.SearchResources(_projectId, query));

        // Then the results are "<results>" (exact ordered set from the Examples row).
        var expected = results
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        Assert.Equal(expected, titles);
    }

    // Scenario: Search is case-insensitive
    [Fact]
    [Trait("Category", "integration")]
    public void Search_is_case_insensitive()
    {
        // When I search the project for "FOUNDRY"
        var titles = Titles(_search.SearchResources(_projectId, "FOUNDRY"));

        // Then the results include "Foundry note"
        Assert.Contains("Foundry note", titles);
    }

    // Scenario: Search is scoped to the current project only
    [Fact]
    [Trait("Category", "integration")]
    public void Search_is_scoped_to_the_current_project_only()
    {
        // Given another project with a resource containing "foundry"
        var projects = new ProjectService(_store, new SettingsService(_store));
        var otherProjectId = projects.Create("Displays 2026").Id;
        var otherResource = _resources.AddText(otherProjectId, "Other foundry brief", "A rival foundry expanded output.");

        // When I search project "Semiconductors 2026" for "foundry"
        var hits = _search.SearchResources(_projectId, "foundry");

        // Then results come only from "Semiconductors 2026"
        Assert.DoesNotContain(otherResource.Id, hits.Select(h => h.Id));
        Assert.DoesNotContain("Other foundry brief", Titles(hits));
        Assert.All(hits, h => Assert.NotEqual(otherResource.Id, h.Id));
        Assert.Contains("Foundry note", Titles(hits));
    }

    // Scenario: A newly added resource becomes searchable
    [Fact]
    [Trait("Category", "integration")]
    public void A_newly_added_resource_becomes_searchable()
    {
        // When I add a resource with text "Export controls tightened in 2025."
        var added = _resources.AddText(_projectId, "Export note", "Export controls tightened in 2025.");

        // And I search the project for "export"
        var hits = _search.SearchResources(_projectId, "export");

        // Then the new resource is in the results
        Assert.Contains(added.Id, hits.Select(h => h.Id));
    }

    // Scenario: Re-extracting updates what the resource matches
    [Fact]
    [Trait("Category", "integration")]
    public void Re_extracting_updates_what_the_resource_matches()
    {
        // Given a file resource currently matching "draft"
        var filePath = Path.Combine(_dataDir, "note.txt");
        File.WriteAllText(filePath, "This is a draft of the market outlook.");
        var added = _resources.AddFile(_projectId, filePath);
        Assert.Contains(added.Resource.Id, _search.SearchResources(_projectId, "draft").Select(h => h.Id));

        // When its re-extracted text no longer contains "draft" but contains "final"
        File.WriteAllText(added.Resource.BlobPath!, "This is the final market outlook.");
        _resources.ReExtract(added.Resource.Id);

        // And I search for "final" / Then the resource is in the results
        Assert.Contains(added.Resource.Id, _search.SearchResources(_projectId, "final").Select(h => h.Id));

        // And searching for "draft" no longer returns it
        Assert.DoesNotContain(added.Resource.Id, _search.SearchResources(_projectId, "draft").Select(h => h.Id));
    }

    // Scenario: Removing a resource drops it from the index
    [Fact]
    [Trait("Category", "integration")]
    public void Removing_a_resource_drops_it_from_the_index()
    {
        // Given a resource matching "obsolete"
        var added = _resources.AddText(_projectId, "Legacy note", "This methodology is obsolete now.");
        Assert.NotEmpty(_search.SearchResources(_projectId, "obsolete"));

        // When I remove it
        _resources.Remove(added.Id);

        // And I search for "obsolete" / Then no results are returned
        Assert.Empty(_search.SearchResources(_projectId, "obsolete"));
    }

    // Scenario: A query with no matches returns an empty result set  (@unit)
    [Fact]
    public void A_query_with_no_matches_returns_an_empty_result_set()
    {
        // When I search the project for "nonexistentterm"
        var hits = _search.SearchResources(_projectId, "nonexistentterm");

        // Then no results are returned
        Assert.Empty(hits);
    }

    // Scenario: The search service is designed to extend to conversations and artifacts
    [Fact]
    [Trait("Category", "integration")]
    public void The_search_service_is_designed_to_extend_to_conversations_and_artifacts()
    {
        // Given FTS tables exist for message content and artifact version content
        Assert.True(FtsTableExists("MessageFts"), "Expected MessageFts to exist.");
        Assert.True(FtsTableExists("ArtifactVersionFts"), "Expected ArtifactVersionFts to exist.");

        // Then the search service can query those content types under the same project scope
        // (the same ISearchService exposes project-scoped message/artifact search; with no message
        // or artifact content yet these return an empty — but valid — project-scoped result set).
        Assert.Empty(_search.SearchMessages(_projectId, "foundry"));
        Assert.Empty(_search.SearchArtifacts(_projectId, "foundry"));
    }

    private bool FtsTableExists(string tableName)
    {
        using var conn = _store.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $n;";
        cmd.Parameters.AddWithValue("$n", tableName);
        var sql = Convert.ToString(cmd.ExecuteScalar());
        return sql is not null && sql.Contains("fts5", StringComparison.OrdinalIgnoreCase);
    }
}

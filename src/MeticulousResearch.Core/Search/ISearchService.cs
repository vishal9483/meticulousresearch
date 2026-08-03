namespace MeticulousResearch.Core.Search;

/// <summary>
/// Project-scoped full-text search over the app's SQLite FTS5 index (SPEC §3.1, §5). This M1 slice
/// searches <em>resource extracted text</em>; the interface is deliberately shaped so the
/// <c>conversations</c> (M2) and <c>artifacts</c> (M3) features can light up
/// <see cref="SearchMessages"/> / <see cref="SearchArtifacts"/> over the FTS tables that already
/// exist, under the same project scope, without a redesign.
/// <para>
/// The service only <em>reads</em> the FTS5 virtual tables and their sync triggers (owned by
/// <c>data-store-migrations</c> §5). Every query is scoped to a single project by joining the FTS
/// hit back to its base row, so no result can leak across projects. Matching is case-insensitive
/// and results are relevance-ranked (bm25). User input is sanitised into a safe FTS query so
/// special syntax never raises a query error.
/// </para>
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches the project's resource extracted text for <paramref name="query"/> and returns the
    /// matching resources ordered most-relevant first. Results are scoped to
    /// <paramref name="projectId"/> only. An empty/whitespace query or one with no matches returns
    /// an empty list (never <c>null</c>).
    /// </summary>
    /// <param name="projectId">The project whose resources are searched.</param>
    /// <param name="query">The raw user search text; sanitised before it reaches FTS5.</param>
    IReadOnlyList<SearchHit> SearchResources(string projectId, string query);

    /// <summary>
    /// Searches the project's conversation message content for <paramref name="query"/>
    /// (SPEC §3.1). Backed by the existing <c>MessageFts</c> table; returns an empty list until
    /// <c>conversations</c> (M2) populates message content. Project-scoped via the owning
    /// conversation.
    /// </summary>
    IReadOnlyList<SearchHit> SearchMessages(string projectId, string query);

    /// <summary>
    /// Searches the project's artifact version content for <paramref name="query"/> (SPEC §3.1).
    /// Backed by the existing <c>ArtifactVersionFts</c> table; returns an empty list until
    /// <c>artifacts</c> (M3) populates artifact content. Project-scoped via the owning artifact.
    /// </summary>
    IReadOnlyList<SearchHit> SearchArtifacts(string projectId, string query);
}

using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;

namespace MeticulousResearch.Core.Search;

/// <summary>
/// <see cref="ISearchService"/> over the SQLite FTS5 index (SPEC §3.1, §5). Reads the
/// external-content virtual tables (<c>ResourceFts</c>, <c>MessageFts</c>,
/// <c>ArtifactVersionFts</c>) and their sync triggers owned by <c>data-store-migrations</c> — it
/// never creates a parallel index. Each search joins the FTS hit back to its base row and filters
/// by project so results cannot leak across projects, orders by FTS5 <c>rank</c> (bm25) for
/// relevance, and sanitises the user's text into a safe MATCH expression.
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly DataStore _store;

    /// <summary>Creates the search service over a <see cref="DataStore"/>.</summary>
    public SearchService(DataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public IReadOnlyList<SearchHit> SearchResources(string projectId, string query) =>
        Search(
            SearchContentType.Resource,
            projectId,
            query,
            ftsTable: "ResourceFts",
            sql: """
                SELECT r.id, r.title, snippet(ResourceFts, 1, '[', ']', '…', 8)
                FROM ResourceFts
                JOIN Resource r ON r.rowid = ResourceFts.rowid
                WHERE r.project_id = $pid AND ResourceFts MATCH $q
                ORDER BY rank;
                """);

    /// <inheritdoc />
    public IReadOnlyList<SearchHit> SearchMessages(string projectId, string query) =>
        Search(
            SearchContentType.Message,
            projectId,
            query,
            ftsTable: "MessageFts",
            sql: """
                SELECT m.id, c.title, snippet(MessageFts, 0, '[', ']', '…', 8)
                FROM MessageFts
                JOIN Message m ON m.rowid = MessageFts.rowid
                JOIN Conversation c ON c.id = m.conversation_id
                WHERE c.project_id = $pid AND MessageFts MATCH $q
                ORDER BY rank;
                """);

    /// <inheritdoc />
    public IReadOnlyList<SearchHit> SearchArtifacts(string projectId, string query) =>
        Search(
            SearchContentType.Artifact,
            projectId,
            query,
            ftsTable: "ArtifactVersionFts",
            sql: """
                SELECT av.id, a.title, snippet(ArtifactVersionFts, 0, '[', ']', '…', 8)
                FROM ArtifactVersionFts
                JOIN ArtifactVersion av ON av.rowid = ArtifactVersionFts.rowid
                JOIN Artifact a ON a.id = av.artifact_id
                WHERE a.project_id = $pid AND ArtifactVersionFts MATCH $q
                ORDER BY rank;
                """);

    private List<SearchHit> Search(
        SearchContentType contentType, string projectId, string query, string ftsTable, string sql)
    {
        var results = new List<SearchHit>();
        if (string.IsNullOrWhiteSpace(projectId))
            return results;

        var match = BuildMatchExpression(query);
        if (match.Length == 0)
            return results;

        using var conn = _store.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$pid", projectId);
        cmd.Parameters.AddWithValue("$q", match);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var title = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var snippet = reader.IsDBNull(2) ? null : reader.GetString(2);
            results.Add(new SearchHit(contentType, id, title, snippet));
        }

        return results;
    }

    /// <summary>
    /// Sanitises raw user input into a safe FTS5 MATCH expression: each whitespace-separated term is
    /// wrapped in double quotes (with embedded quotes doubled) so FTS5 syntax characters
    /// (<c>* : ( ) " ^ -</c> etc.) are treated as literal text and can never raise a query-syntax
    /// error. Terms are ANDed together (implicit FTS5 conjunction). Returns an empty string when the
    /// query has no searchable term.
    /// </summary>
    internal static string BuildMatchExpression(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "";

        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();
        foreach (var term in terms)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append('"').Append(term.Replace("\"", "\"\"")).Append('"');
        }

        return builder.ToString();
    }
}

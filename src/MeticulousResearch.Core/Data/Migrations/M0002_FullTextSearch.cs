using Microsoft.Data.Sqlite;

namespace MeticulousResearch.Core.Data.Migrations;

/// <summary>
/// Migration 2 — FTS5 virtual tables over the searchable content (SPEC §5): resource extracted
/// text, message content, and artifact version content. Each FTS table is an external-content
/// index (<c>content=</c>) keyed to its base table's rowid, kept in sync by AFTER
/// INSERT/UPDATE/DELETE triggers. EF cannot model virtual tables, so this is raw SQL.
/// <para>
/// <c>ResourceFts</c> indexes <c>Resource(title, extracted_text)</c> — the denormalized body
/// text (see <see cref="M0001_InitialSchema"/>), so downstream full-text search matches resource
/// BODY text, not just the title. <c>MessageFts</c> and <c>ArtifactVersionFts</c> index the real
/// <c>content</c> column of their base tables. The sync-trigger contract is owned here; the
/// full-text-search feature only reads and asserts sync.
/// </para>
/// </summary>
public sealed class M0002_FullTextSearch : IMigration
{
    public int Version => 2;
    public string Name => "FullTextSearch";

    public void Up(SqliteConnection connection)
    {
        // Resource extracted text. Canonical body text lives on disk in extracted.txt
        // (Resource.extracted_path), and is denormalized into the Resource.extracted_text column
        // so this external-content FTS table can index the searchable BODY text (not the file
        // path). ResourceFts therefore indexes title + extracted_text; the triggers below keep it
        // in sync on insert/update/delete. Project scoping is done by the search feature joining
        // FTS hits back to Resource on rowid (see full-text-search/phase.md) — this feature owns
        // only the DDL/triggers.
        CreateFts(connection,
            ftsTable: "ResourceFts",
            baseTable: "Resource",
            columns: new[] { "title", "extracted_text" });

        CreateFts(connection,
            ftsTable: "MessageFts",
            baseTable: "Message",
            columns: new[] { "content" });

        CreateFts(connection,
            ftsTable: "ArtifactVersionFts",
            baseTable: "ArtifactVersion",
            columns: new[] { "content" });
    }

    /// <summary>
    /// Creates an external-content FTS5 table mirroring <paramref name="columns"/> of
    /// <paramref name="baseTable"/> (matched on rowid), plus insert/delete/update sync triggers.
    /// </summary>
    private static void CreateFts(SqliteConnection connection, string ftsTable, string baseTable, string[] columns)
    {
        var colList = string.Join(", ", columns);
        var newValues = string.Join(", ", columns.Select(c => "new." + c));

        Exec(connection,
            $"CREATE VIRTUAL TABLE {ftsTable} USING fts5({colList}, content='{baseTable}', content_rowid='rowid');");

        Exec(connection, $"""
            CREATE TRIGGER {baseTable}_ai AFTER INSERT ON {baseTable} BEGIN
                INSERT INTO {ftsTable}(rowid, {colList}) VALUES (new.rowid, {newValues});
            END;
            """);

        Exec(connection, $"""
            CREATE TRIGGER {baseTable}_ad AFTER DELETE ON {baseTable} BEGIN
                INSERT INTO {ftsTable}({ftsTable}, rowid, {colList})
                VALUES ('delete', old.rowid, {string.Join(", ", columns.Select(c => "old." + c))});
            END;
            """);

        Exec(connection, $"""
            CREATE TRIGGER {baseTable}_au AFTER UPDATE ON {baseTable} BEGIN
                INSERT INTO {ftsTable}({ftsTable}, rowid, {colList})
                VALUES ('delete', old.rowid, {string.Join(", ", columns.Select(c => "old." + c))});
                INSERT INTO {ftsTable}(rowid, {colList}) VALUES (new.rowid, {newValues});
            END;
            """);
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}

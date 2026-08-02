using Microsoft.Data.Sqlite;

namespace MeticulousResearch.Core.Data.Migrations;

/// <summary>
/// Migration 2 — FTS5 virtual tables over the searchable content (SPEC §5): resource extracted
/// text, message content, and artifact version content. Each FTS table is an external-content
/// index (<c>content=</c>) keyed to its base table's rowid, kept in sync by AFTER
/// INSERT/UPDATE/DELETE triggers. EF cannot model virtual tables, so this is raw SQL.
/// </summary>
public sealed class M0002_FullTextSearch : IMigration
{
    public int Version => 2;
    public string Name => "FullTextSearch";

    public void Up(SqliteConnection connection)
    {
        // Resource extracted text. The searchable text is loaded into extracted_path files on
        // disk; we index a denormalized "extracted_text" column populated by later features. To
        // keep the FTS contract concrete now, index the columns present on the base row and expose
        // a stable "text" column the search feature will target.
        CreateFts(connection,
            ftsTable: "ResourceFts",
            baseTable: "Resource",
            columns: new[] { "title", "extracted_path" });

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

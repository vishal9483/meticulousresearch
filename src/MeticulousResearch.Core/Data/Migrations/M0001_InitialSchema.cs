using Microsoft.Data.Sqlite;

namespace MeticulousResearch.Core.Data.Migrations;

/// <summary>
/// Migration 1 — the initial relational schema (SPEC §5): Project, Resource, Conversation,
/// Message, Artifact, ArtifactVersion, Setting. Column names are snake_case to match the SPEC
/// and are pinned; downstream features assert on specific columns.
/// </summary>
public sealed class M0001_InitialSchema : IMigration
{
    public int Version => 1;
    public string Name => "InitialSchema";

    public void Up(SqliteConnection connection)
    {
        Exec(connection, """
            CREATE TABLE Project (
                id                   TEXT NOT NULL PRIMARY KEY,
                name                 TEXT NOT NULL,
                description          TEXT NULL,
                custom_instructions  TEXT NULL,
                default_model        TEXT NULL,
                color                TEXT NULL,
                archived             INTEGER NOT NULL DEFAULT 0,
                created_at           TEXT NOT NULL,
                updated_at           TEXT NOT NULL
            );
            """);

        Exec(connection, """
            CREATE TABLE Resource (
                id              TEXT NOT NULL PRIMARY KEY,
                project_id      TEXT NOT NULL,
                title           TEXT NOT NULL,
                type            TEXT NOT NULL,
                source_uri      TEXT NULL,
                blob_path       TEXT NULL,
                extracted_path  TEXT NULL,
                byte_size       INTEGER NULL,
                token_estimate  INTEGER NULL,
                enabled         INTEGER NOT NULL DEFAULT 1,
                created_at      TEXT NOT NULL,
                updated_at      TEXT NOT NULL,
                FOREIGN KEY (project_id) REFERENCES Project (id) ON DELETE CASCADE
            );
            """);
        Exec(connection, "CREATE INDEX IX_Resource_project_id ON Resource (project_id);");

        Exec(connection, """
            CREATE TABLE Conversation (
                id             TEXT NOT NULL PRIMARY KEY,
                project_id     TEXT NOT NULL,
                title          TEXT NOT NULL,
                model_default  TEXT NULL,
                created_at     TEXT NOT NULL,
                updated_at     TEXT NOT NULL,
                FOREIGN KEY (project_id) REFERENCES Project (id) ON DELETE CASCADE
            );
            """);
        Exec(connection, "CREATE INDEX IX_Conversation_project_id ON Conversation (project_id);");

        Exec(connection, """
            CREATE TABLE Message (
                id                   TEXT NOT NULL PRIMARY KEY,
                conversation_id      TEXT NOT NULL,
                role                 TEXT NOT NULL,
                content              TEXT NOT NULL,
                model                TEXT NULL,
                tokens_in            INTEGER NOT NULL DEFAULT 0,
                tokens_out           INTEGER NOT NULL DEFAULT 0,
                tokens_cache_read    INTEGER NOT NULL DEFAULT 0,
                tokens_cache_write   INTEGER NOT NULL DEFAULT 0,
                cost_usd             REAL NULL,
                latency_ms           INTEGER NULL,
                resource_scope_json  TEXT NULL,
                created_at           TEXT NOT NULL,
                FOREIGN KEY (conversation_id) REFERENCES Conversation (id) ON DELETE CASCADE
            );
            """);
        Exec(connection, "CREATE INDEX IX_Message_conversation_id ON Message (conversation_id);");

        Exec(connection, """
            CREATE TABLE Artifact (
                id                  TEXT NOT NULL PRIMARY KEY,
                project_id          TEXT NOT NULL,
                title               TEXT NOT NULL,
                type                TEXT NOT NULL,
                current_version_id  TEXT NULL,
                created_at          TEXT NOT NULL,
                updated_at          TEXT NOT NULL,
                FOREIGN KEY (project_id) REFERENCES Project (id) ON DELETE CASCADE
            );
            """);
        Exec(connection, "CREATE INDEX IX_Artifact_project_id ON Artifact (project_id);");

        Exec(connection, """
            CREATE TABLE ArtifactVersion (
                id                   TEXT NOT NULL PRIMARY KEY,
                artifact_id          TEXT NOT NULL,
                version_no           INTEGER NOT NULL,
                content              TEXT NOT NULL,
                content_format       TEXT NULL,
                model                TEXT NULL,
                prompt               TEXT NULL,
                tokens_in            INTEGER NOT NULL DEFAULT 0,
                tokens_out           INTEGER NOT NULL DEFAULT 0,
                cost_usd             REAL NULL,
                resource_scope_json  TEXT NULL,
                created_by           TEXT NOT NULL,
                created_at           TEXT NOT NULL,
                FOREIGN KEY (artifact_id) REFERENCES Artifact (id) ON DELETE CASCADE
            );
            """);
        Exec(connection, "CREATE INDEX IX_ArtifactVersion_artifact_id ON ArtifactVersion (artifact_id);");
        Exec(connection, "CREATE UNIQUE INDEX UX_ArtifactVersion_artifact_version ON ArtifactVersion (artifact_id, version_no);");

        Exec(connection, """
            CREATE TABLE Setting (
                key    TEXT NOT NULL PRIMARY KEY,
                value  TEXT NULL
            );
            """);
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}

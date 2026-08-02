using System.Data;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Data;

/// <summary>
/// Faithful xUnit translation of docs/features/data-store-migrations/tests.md.
/// Every scenario is <c>@unit @integration</c>: it runs in the headless gate (no Category
/// trait excluded by the filter) and carries [Trait("Category","integration")].
/// </summary>
public sealed class DataStoreTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));

    public DataStoreTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-datastore-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; a locked file must not fail the test run.
        }
    }

    private DataStore NewStore(string? dir = null) => new(_clock, dir ?? _dataDir);

    private static bool TableExists(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table','view') AND name = $n;";
        cmd.Parameters.AddWithValue("$n", table);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static HashSet<string> ColumnsOf(SqliteConnection conn, string table)
    {
        var cols = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{table}');";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            cols.Add(r.GetString(r.GetOrdinal("name")));
        return cols;
    }

    // ---------------------------------------------------------------- Fresh install

    // Scenario: A fresh database is created with the current schema
    [Fact]
    [Trait("Category", "integration")]
    public void FreshDatabase_isCreated_withCurrentSchema()
    {
        // Given no database file exists in the data directory
        var dbPath = Path.Combine(_dataDir, DataStore.DatabaseFileName);
        Assert.False(File.Exists(dbPath));

        // When the data store initializes
        using var store = NewStore();
        store.Initialize();

        // Then a "db.sqlite" file is created
        Assert.Equal("db.sqlite", DataStore.DatabaseFileName);
        Assert.True(File.Exists(dbPath));

        // And the schema version equals the latest migration version
        Assert.Equal(DataStore.LatestSchemaVersion, store.GetSchemaVersion());

        // And WAL journal mode is enabled
        using var conn = store.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = Convert.ToString(cmd.ExecuteScalar());
        Assert.Equal("wal", mode, ignoreCase: true);
    }

    // Scenario Outline: All core tables exist after initialization
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("Project")]
    [InlineData("Resource")]
    [InlineData("Conversation")]
    [InlineData("Message")]
    [InlineData("Artifact")]
    [InlineData("ArtifactVersion")]
    [InlineData("Setting")]
    public void AllCoreTables_exist_afterInitialization(string table)
    {
        // Given an initialized data store
        using var store = NewStore();
        store.Initialize();

        // Then a table named "<table>" exists
        using var conn = store.OpenConnection();
        Assert.True(TableExists(conn, table), $"Expected table '{table}' to exist.");
    }

    // Scenario Outline: Full-text search virtual tables exist
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("resource extracted text", "ResourceFts")]
    [InlineData("message content", "MessageFts")]
    [InlineData("artifact version content", "ArtifactVersionFts")]
    public void FtsVirtualTables_exist(string content, string ftsTable)
    {
        // Given an initialized data store
        using var store = NewStore();
        store.Initialize();

        using var conn = store.OpenConnection();

        // Then an FTS5 virtual table indexing "<content>" exists
        Assert.True(TableExists(conn, ftsTable), $"Expected FTS table '{ftsTable}' for {content} to exist.");

        // And it is genuinely an FTS5 virtual table (not a plain table).
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name = $n;";
        cmd.Parameters.AddWithValue("$n", ftsTable);
        var sql = Convert.ToString(cmd.ExecuteScalar()) ?? "";
        Assert.Contains("fts5", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Scenario: Full-text search virtual tables exist — round-trip strengthening.
    // The exists-check above proves ResourceFts is an fts5 table; this proves the trigger
    // actually indexes the resource BODY text (Resource.extracted_text), so a MATCH on a word
    // that appears only in the body — never in the title or any file path — returns the row.
    // This is the assertion whose absence let the earlier "index the path, not the body" leak
    // through review. Strengthens the "@unit @integration" FTS scenario.
    [Fact]
    [Trait("Category", "integration")]
    public void ResourceFts_indexes_extractedBodyText_notJustTitle()
    {
        using var store = NewStore();
        store.Initialize();

        using var conn = store.OpenConnection();

        // Seed a Project (FK parent) and a Resource whose body text contains a word that appears
        // nowhere in the title nor in extracted_path — proving BODY text is what gets indexed.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Project (id, name, archived, created_at, updated_at) " +
                "VALUES ('P-fts', 'FTS', 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Resource (id, project_id, title, type, extracted_path, extracted_text, enabled, created_at, updated_at) " +
                "VALUES ('R-fts', 'P-fts', 'Wafer note', 'text', 'projects/P-fts/resources/R-fts/extracted.txt', " +
                "$body, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');";
            cmd.Parameters.AddWithValue("$body", "Wafer starts rose sharply across leading nodes.");
            cmd.ExecuteNonQuery();
        }

        // A MATCH on a body-only word returns the resource (proves body text is indexed).
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT r.title FROM ResourceFts f JOIN Resource r ON r.rowid = f.rowid WHERE ResourceFts MATCH $q;";
            cmd.Parameters.AddWithValue("$q", "sharply");
            Assert.Equal("Wafer note", Convert.ToString(cmd.ExecuteScalar()));
        }

        // A MATCH on the file-path token 'extracted' must NOT return it — the path is not indexed.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ResourceFts WHERE ResourceFts MATCH $q;";
            cmd.Parameters.AddWithValue("$q", "projects");
            Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar()));
        }

        // Sync on UPDATE: re-extraction replaces the body; old word gone, new word found.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Resource SET extracted_text = $body WHERE id = 'R-fts';";
            cmd.Parameters.AddWithValue("$body", "Foundry capacity grew twelve percent.");
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ResourceFts WHERE ResourceFts MATCH $q;";
            cmd.Parameters.AddWithValue("$q", "sharply");
            Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ResourceFts WHERE ResourceFts MATCH $q;";
            cmd.Parameters.AddWithValue("$q", "foundry");
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
        }

        // Sync on DELETE: removing the resource drops it from the index.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Resource WHERE id = 'R-fts';";
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ResourceFts WHERE ResourceFts MATCH $q;";
            cmd.Parameters.AddWithValue("$q", "foundry");
            Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
    }

    // ---------------------------------------------------------------- Schema shape

    // Scenario: Message table records token and cost columns
    [Fact]
    [Trait("Category", "integration")]
    public void MessageTable_records_tokenAndCostColumns()
    {
        using var store = NewStore();
        store.Initialize();

        using var conn = store.OpenConnection();
        var cols = ColumnsOf(conn, "Message");

        foreach (var expected in new[]
                 {
                     "tokens_in", "tokens_out", "tokens_cache_read", "tokens_cache_write",
                     "cost_usd", "model", "latency_ms", "resource_scope_json",
                 })
            Assert.Contains(expected, cols);
    }

    // Scenario: ArtifactVersion table records provenance columns
    [Fact]
    [Trait("Category", "integration")]
    public void ArtifactVersionTable_records_provenanceColumns()
    {
        using var store = NewStore();
        store.Initialize();

        using var conn = store.OpenConnection();
        var cols = ColumnsOf(conn, "ArtifactVersion");

        foreach (var expected in new[]
                 {
                     "version_no", "content", "content_format", "model", "prompt",
                     "tokens_in", "tokens_out", "cost_usd", "resource_scope_json", "created_by",
                 })
            Assert.Contains(expected, cols);
    }

    // ---------------------------------------------------------------- Migrations

    // Scenario: An out-of-date database is upgraded in place
    [Fact]
    [Trait("Category", "integration")]
    public void OutOfDateDatabase_isUpgraded_inPlace_preservingRows()
    {
        // Given a database at an older schema version with existing rows.
        // We simulate v1 by running only the first migration, then inserting a row.
        Directory.CreateDirectory(_dataDir);
        var dbPath = Path.Combine(_dataDir, DataStore.DatabaseFileName);
        using (var seed = NewStore())
        {
            seed.InitializeToVersion(1);
            Assert.Equal(1, seed.GetSchemaVersion());
            Assert.True(DataStore.LatestSchemaVersion >= 1);

            using var conn = seed.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO Project (id, name, description, custom_instructions, default_model, color, archived, created_at, updated_at) " +
                "VALUES ('P-legacy', 'Legacy', NULL, NULL, NULL, NULL, 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');";
            cmd.ExecuteNonQuery();
        }

        // When the data store initializes
        using var store = NewStore();
        store.Initialize();

        // Then all pending migrations run in order
        // And the schema version equals the latest migration version
        Assert.Equal(DataStore.LatestSchemaVersion, store.GetSchemaVersion());

        // And pre-existing rows are preserved
        using (var conn = store.OpenConnection())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM Project WHERE id = 'P-legacy';";
            Assert.Equal("Legacy", Convert.ToString(cmd.ExecuteScalar()));
        }
    }

    // Scenario: Migrations are idempotent on an up-to-date database
    [Fact]
    [Trait("Category", "integration")]
    public void Migrations_areIdempotent_onUpToDateDatabase()
    {
        // Given a database already at the latest schema version
        using (var first = NewStore())
        {
            first.Initialize();
            Assert.Equal(DataStore.LatestSchemaVersion, first.GetSchemaVersion());

            using var conn = first.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO Project (id, name, archived, created_at, updated_at) " +
                "VALUES ('P-keep', 'Keep', 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');";
            cmd.ExecuteNonQuery();
        }

        // When the data store initializes again
        using var store = NewStore();
        var applied = store.Initialize();

        // Then no migration runs
        Assert.Empty(applied);
        Assert.Equal(DataStore.LatestSchemaVersion, store.GetSchemaVersion());

        // And no data changes
        using (var conn = store.OpenConnection())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Project WHERE id = 'P-keep';";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
    }

    // ---------------------------------------------------------------- Reliability & layout

    // Scenario: Integrity check passes on a healthy database
    [Fact]
    [Trait("Category", "integration")]
    public void IntegrityCheck_passes_onHealthyDatabase()
    {
        // Given an initialized data store
        using var store = NewStore();
        store.Initialize();

        // When an integrity check runs / Then it reports "ok"
        Assert.Equal("ok", store.IntegrityCheck());
    }

    // Scenario: The per-project file layout is created on demand
    [Fact]
    [Trait("Category", "integration")]
    public void PerProjectFileLayout_isCreated_onDemand()
    {
        // Given an initialized data store
        using var store = NewStore();
        store.Initialize();

        // When a project with id "P1" needs resource storage
        var dir = store.FileStore.GetProjectResourcesDirectory("P1");

        // Then a directory "projects/P1/resources" exists under the data directory
        var expected = Path.Combine(_dataDir, "projects", "P1", "resources");
        Assert.Equal(expected, dir);
        Assert.True(Directory.Exists(expected));
    }

    // Scenario: Data directory location is configurable
    [Fact]
    [Trait("Category", "integration")]
    public void DataDirectoryLocation_isConfigurable()
    {
        // Given a configured data directory at a custom path
        var custom = Path.Combine(Path.GetTempPath(), "mr-custom-" + Guid.NewGuid().ToString("N"));
        try
        {
            // When the data store initializes
            using var store = NewStore(custom);
            store.Initialize();
            store.FileStore.GetProjectResourcesDirectory("PX");

            // Then the database and projects folder are created under that custom path
            Assert.True(File.Exists(Path.Combine(custom, DataStore.DatabaseFileName)));
            Assert.True(Directory.Exists(Path.Combine(custom, "projects")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(custom)) Directory.Delete(custom, true); } catch (IOException) { }
        }
    }
}

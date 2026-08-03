using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data.Migrations;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Data;

/// <summary>
/// The persistence foundation (SPEC §5, §8). Owns the SQLite database file, connection setup
/// (WAL, foreign keys, busy-timeout), the versioned migration runner, integrity checks, and the
/// per-project file layout. <see cref="Initialize"/> is the single entry point downstream features
/// call at startup; afterwards they consume <see cref="FileStore"/>, <see cref="OpenConnection"/>,
/// and <see cref="CreateDbContext"/>.
/// </summary>
public sealed class DataStore : IDisposable
{
    /// <summary>The database file name under the data directory (SPEC §5).</summary>
    public const string DatabaseFileName = "db.sqlite";

    /// <summary>Busy-timeout applied to every connection (milliseconds).</summary>
    public const int BusyTimeoutMs = 5000;

    private readonly IClock _clock;
    private readonly MigrationRunner _runner;

    /// <summary>Creates a data store rooted at <paramref name="dataDirectory"/>.</summary>
    /// <param name="clock">Injected clock for created_at/updated_at (TESTING-STRATEGY §4).</param>
    /// <param name="dataDirectory">
    /// The configurable root directory (tests pass a temp dir; the app defaults to
    /// <c>%LOCALAPPDATA%/MeticulousResearch</c>).
    /// </param>
    public DataStore(IClock clock, string dataDirectory)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("Data directory must be a non-empty path.", nameof(dataDirectory));

        DataDirectory = Path.GetFullPath(dataDirectory);
        FileStore = new ProjectFileStore(DataDirectory);
        _runner = new MigrationRunner(MigrationRunner.All);
    }

    /// <summary>The clock used for timestamping (exposed so repositories can share it).</summary>
    public IClock Clock => _clock;

    /// <summary>The resolved absolute data directory.</summary>
    public string DataDirectory { get; }

    /// <summary>The absolute path to the SQLite database file.</summary>
    public string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>The on-disk file layout service (SPEC §5).</summary>
    public IProjectFileStore FileStore { get; }

    /// <summary>The latest schema version known to the application (the migration target).</summary>
    public static int LatestSchemaVersion => MigrationRunner.LatestVersion;

    /// <summary>
    /// Ensures the data directory exists, opens the database with WAL + foreign keys +
    /// busy-timeout, and runs all pending migrations (preserving existing data). Returns the
    /// versions applied this call (empty when already up to date). Idempotent.
    /// </summary>
    public IReadOnlyList<int> Initialize() => InitializeCore(targetVersion: null);

    /// <summary>
    /// Test/upgrade seam: initialize the store but stop at <paramref name="targetVersion"/> so an
    /// "older" database can be created and then upgraded on a later <see cref="Initialize"/> call.
    /// </summary>
    public IReadOnlyList<int> InitializeToVersion(int targetVersion) => InitializeCore(targetVersion);

    private IReadOnlyList<int> InitializeCore(int? targetVersion)
    {
        Directory.CreateDirectory(DataDirectory);

        using var conn = OpenConnection();
        return _runner.MigrateUp(conn, targetVersion);
    }

    /// <summary>The schema version currently recorded in the database.</summary>
    public int GetSchemaVersion()
    {
        using var conn = OpenConnection();
        return _runner.GetCurrentVersion(conn);
    }

    /// <summary>
    /// Opens a new SQLite connection to the database with the standard PRAGMAs applied:
    /// WAL journal mode, foreign keys on, and a busy-timeout. Callers own the returned connection.
    /// </summary>
    public SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();
        ApplyPragmas(conn);
        return conn;
    }

    /// <summary>
    /// Creates an <see cref="AppDbContext"/> bound to this store's database, sharing a
    /// PRAGMA-configured connection. Callers own its lifetime (dispose it).
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var conn = OpenConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Runs <c>PRAGMA integrity_check</c> and returns its first row. A healthy database returns
    /// the literal <c>"ok"</c> (SPEC §8 data safety).
    /// </summary>
    public string IntegrityCheck()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(cmd.ExecuteScalar());
        return result ?? "";
    }

    private static void ApplyPragmas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        // WAL for concurrent readers + crash safety; foreign_keys enforce referential integrity;
        // busy_timeout so briefly-locked writes wait rather than fail.
        cmd.CommandText =
            "PRAGMA journal_mode=WAL; " +
            "PRAGMA foreign_keys=ON; " +
            $"PRAGMA busy_timeout={BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Clears the ADO.NET connection pool for <em>this store's</em> database only, releasing its
    /// pooled connections and file handles so a temp database can be deleted. Unlike
    /// <see cref="SqliteConnection.ClearAllPools"/>, this is scoped to this store's connection
    /// string and will not dispose connections owned by other <see cref="DataStore"/> instances
    /// running concurrently (e.g. parallel test classes).
    /// </summary>
    public void ClearConnectionPool()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        SqliteConnection.ClearPool(new SqliteConnection(connectionString));
    }

    /// <summary>No process-wide resources are held; pooled connections are released on dispose.</summary>
    public void Dispose()
    {
        // Connections are opened per-call and disposed by callers; nothing persistent to release.
    }
}

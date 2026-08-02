using Microsoft.Data.Sqlite;

namespace MeticulousResearch.Core.Data.Migrations;

/// <summary>
/// Ordered, idempotent, forward-only migration runner. Tracks the applied schema version in a
/// dedicated <c>schema_version</c> table and applies only migrations whose version exceeds the
/// current one, in ascending order, each inside its own transaction. Running against an
/// up-to-date database applies nothing and touches no data.
/// </summary>
public sealed class MigrationRunner
{
    private readonly IReadOnlyList<IMigration> _migrations;

    /// <summary>Creates a runner over the given migrations (order is derived from Version).</summary>
    public MigrationRunner(IEnumerable<IMigration> migrations)
    {
        _migrations = migrations.OrderBy(m => m.Version).ToList();
    }

    /// <summary>The complete, ordered set of migrations known to the application.</summary>
    public static IReadOnlyList<IMigration> All { get; } = new IMigration[]
    {
        new M0001_InitialSchema(),
        new M0002_FullTextSearch(),
    };

    /// <summary>The highest version across <see cref="All"/> — the "latest" schema version.</summary>
    public static int LatestVersion => All.Max(m => m.Version);

    /// <summary>The current schema version recorded in the database (0 if never migrated).</summary>
    public int GetCurrentVersion(SqliteConnection connection)
    {
        EnsureVersionTable(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Applies all pending migrations up to (and including) <paramref name="targetVersion"/> in
    /// order, preserving existing data. Returns the versions actually applied (empty if none).
    /// A null target means "latest".
    /// </summary>
    public IReadOnlyList<int> MigrateUp(SqliteConnection connection, int? targetVersion = null)
    {
        EnsureVersionTable(connection);
        var current = GetCurrentVersion(connection);
        var target = targetVersion ?? (_migrations.Count == 0 ? 0 : _migrations.Max(m => m.Version));

        var applied = new List<int>();
        foreach (var migration in _migrations)
        {
            if (migration.Version <= current || migration.Version > target)
                continue;

            using var tx = connection.BeginTransaction();
            migration.Up(connection);

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($v, $t);";
                cmd.Parameters.AddWithValue("$v", migration.Version);
                cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            applied.Add(migration.Version);
        }

        return applied;
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_version (
                version     INTEGER NOT NULL PRIMARY KEY,
                applied_at  TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }
}

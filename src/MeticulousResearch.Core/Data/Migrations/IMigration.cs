using Microsoft.Data.Sqlite;

namespace MeticulousResearch.Core.Data.Migrations;

/// <summary>
/// A single, ordered, forward-only schema migration. The runner applies migrations whose
/// <see cref="Version"/> exceeds the database's recorded schema version, in ascending order,
/// inside a transaction, and records the new version. Downstream features add schema changes by
/// adding a new <see cref="IMigration"/> with the next version — never ad-hoc runtime ALTERs.
/// </summary>
public interface IMigration
{
    /// <summary>The schema version this migration brings the database up to. 1-based, contiguous.</summary>
    int Version { get; }

    /// <summary>Short human-readable name (for logs/diagnostics).</summary>
    string Name { get; }

    /// <summary>
    /// Applies the forward migration using the supplied open connection. The runner manages the
    /// enclosing transaction and version bookkeeping; implementations only issue schema DDL/DML.
    /// </summary>
    void Up(SqliteConnection connection);
}

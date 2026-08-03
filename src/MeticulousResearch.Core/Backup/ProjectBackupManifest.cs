namespace MeticulousResearch.Core.Backup;

/// <summary>
/// The manifest embedded in every project backup zip (SPEC §8). Records the on-disk format version
/// of the archive and the schema version the data was produced at, so restore can validate the zip
/// and migrate-or-refuse. Serialized as <c>manifest.json</c> at the root of the archive.
/// </summary>
public sealed class ProjectBackupManifest
{
    /// <summary>The archive layout/format version (independent of the DB schema version).</summary>
    public int FormatVersion { get; set; }

    /// <summary>The database schema version the backed-up rows were produced at.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>The backed-up project's id.</summary>
    public string ProjectId { get; set; } = "";

    /// <summary>The backed-up project's display name (for confirmation UI).</summary>
    public string ProjectName { get; set; } = "";

    /// <summary>UTC instant the backup was produced at (fixed by the injected clock).</summary>
    public string CreatedAt { get; set; } = "";
}

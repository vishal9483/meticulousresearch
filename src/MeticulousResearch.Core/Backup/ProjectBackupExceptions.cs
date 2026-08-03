namespace MeticulousResearch.Core.Backup;

/// <summary>
/// Thrown when a zip presented to <see cref="IProjectBackupService.Restore"/> is corrupt or is not a
/// valid project backup (missing/invalid manifest or data). The data store is left unchanged (SPEC §8).
/// </summary>
public sealed class InvalidProjectBackupException : Exception
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    public InvalidProjectBackupException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public InvalidProjectBackupException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Thrown when a backup was produced at a newer schema version than this app understands and cannot
/// be migrated forward. Restore refuses and leaves the data store consistent (SPEC §8).
/// </summary>
public sealed class IncompatibleBackupVersionException : Exception
{
    /// <summary>Creates the exception describing the version mismatch.</summary>
    public IncompatibleBackupVersionException(int backupSchemaVersion, int appSchemaVersion)
        : base($"This backup was produced at schema version {backupSchemaVersion}, which is newer than " +
               $"this application's schema version {appSchemaVersion}. Update the app to restore it.")
    {
        BackupSchemaVersion = backupSchemaVersion;
        AppSchemaVersion = appSchemaVersion;
    }

    /// <summary>The schema version recorded in the backup.</summary>
    public int BackupSchemaVersion { get; }

    /// <summary>The schema version this application supports.</summary>
    public int AppSchemaVersion { get; }
}

/// <summary>
/// Thrown when a restore targets a project id that already exists and the caller passed
/// <see cref="RestoreConflictPolicy.Prompt"/>. No data is overwritten; the caller should re-invoke
/// with <see cref="RestoreConflictPolicy.RestoreAsCopy"/> or <see cref="RestoreConflictPolicy.Replace"/>
/// (SPEC §8 — never silently overwrite).
/// </summary>
public sealed class ProjectBackupConflictException : Exception
{
    /// <summary>Creates the conflict signal for the given existing project id.</summary>
    public ProjectBackupConflictException(string projectId)
        : base($"A project with id '{projectId}' already exists. Choose to restore as a copy or replace it.")
    {
        ProjectId = projectId;
    }

    /// <summary>The conflicting project id that already exists in the store.</summary>
    public string ProjectId { get; }
}

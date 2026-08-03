namespace MeticulousResearch.Core.Backup;

/// <summary>
/// Backs up a single project to a self-contained zip and restores it into any install (SPEC §8,
/// §9.1(9)). A backup carries the project's relational subset (its rows across <c>Project</c>,
/// <c>Resource</c>, <c>Conversation</c>, <c>Message</c>, <c>Artifact</c>, <c>ArtifactVersion</c>) plus
/// its on-disk resource blobs and extracted text, and a manifest recording the schema/format version.
/// It never includes rows from other projects and never includes any app-level/vault secret (§7.5).
/// Restore is transactional: a corrupt, non-project, or newer-schema archive is refused and leaves the
/// store unchanged, never half-applying.
/// </summary>
public interface IProjectBackupService
{
    /// <summary>
    /// Writes a single backup zip of <paramref name="projectId"/> to <paramref name="destinationZip"/>.
    /// </summary>
    /// <param name="projectId">The project to back up.</param>
    /// <param name="destinationZip">The absolute path of the zip to write.</param>
    /// <exception cref="ArgumentException">An argument is null/empty.</exception>
    /// <exception cref="InvalidOperationException">The project does not exist.</exception>
    void Backup(string projectId, string destinationZip);

    /// <summary>
    /// Restores a project from <paramref name="sourceZip"/>, recreating its rows and files, and
    /// returns the restored project id. When a project with the same id already exists, the caller
    /// must pick <see cref="RestoreConflictPolicy.RestoreAsCopy"/> or
    /// <see cref="RestoreConflictPolicy.Replace"/>; the default <see cref="RestoreConflictPolicy.Prompt"/>
    /// refuses to clobber and throws <see cref="ProjectBackupConflictException"/> so the UI can prompt.
    /// </summary>
    /// <param name="sourceZip">The absolute path of a backup zip.</param>
    /// <param name="conflictPolicy">How to handle an id that already exists.</param>
    /// <returns>The id of the restored project (a new id when restored as a copy).</returns>
    /// <exception cref="ArgumentException">An argument is null/empty.</exception>
    /// <exception cref="InvalidProjectBackupException">The zip is corrupt or not a project backup.</exception>
    /// <exception cref="IncompatibleBackupVersionException">The backup was produced at a newer schema version.</exception>
    /// <exception cref="ProjectBackupConflictException">The id exists and the policy is <see cref="RestoreConflictPolicy.Prompt"/>.</exception>
    string Restore(string sourceZip, RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Prompt);
}

/// <summary>How <see cref="IProjectBackupService.Restore"/> handles an already-existing project id.</summary>
public enum RestoreConflictPolicy
{
    /// <summary>Default: refuse to overwrite and signal the caller to prompt the user.</summary>
    Prompt = 0,

    /// <summary>Recreate the project under a brand-new id, leaving the existing one untouched.</summary>
    RestoreAsCopy = 1,

    /// <summary>Replace the existing project (delete its rows/files first, then restore under the same id).</summary>
    Replace = 2,
}

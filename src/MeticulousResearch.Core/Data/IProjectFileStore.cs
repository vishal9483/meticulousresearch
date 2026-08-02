namespace MeticulousResearch.Core.Data;

/// <summary>
/// Resolves and creates the on-disk file layout for projects, resources, exports, and logs under
/// the configured data directory (SPEC §5 "Files on disk"). Directories are created on demand so
/// callers never have to pre-provision them. All returned paths are absolute.
/// </summary>
public interface IProjectFileStore
{
    /// <summary>The root data directory that everything is rooted under.</summary>
    string DataDirectory { get; }

    /// <summary>
    /// Returns (creating if needed) <c>projects/{projectId}</c> under the data directory.
    /// </summary>
    string GetProjectDirectory(string projectId);

    /// <summary>
    /// Returns (creating if needed) <c>projects/{projectId}/resources</c> under the data directory.
    /// </summary>
    string GetProjectResourcesDirectory(string projectId);

    /// <summary>
    /// Returns (creating if needed) <c>projects/{projectId}/resources/{resourceId}</c>.
    /// </summary>
    string GetResourceDirectory(string projectId, string resourceId);

    /// <summary>Returns (creating if needed) the transient <c>exports/</c> directory.</summary>
    string GetExportsDirectory();

    /// <summary>Returns (creating if needed) the <c>logs/</c> directory.</summary>
    string GetLogsDirectory();
}

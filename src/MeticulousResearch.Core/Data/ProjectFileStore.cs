namespace MeticulousResearch.Core.Data;

/// <summary>
/// Filesystem-backed <see cref="IProjectFileStore"/>. Resolves the SPEC §5 layout under a
/// configurable data directory and creates directories on demand.
/// </summary>
public sealed class ProjectFileStore : IProjectFileStore
{
    /// <summary>Creates a file store rooted at <paramref name="dataDirectory"/>.</summary>
    public ProjectFileStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("Data directory must be a non-empty path.", nameof(dataDirectory));
        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    public string DataDirectory { get; }

    public string GetProjectDirectory(string projectId) =>
        EnsureDir(Path.Combine(DataDirectory, "projects", Require(projectId, nameof(projectId))));

    public string GetProjectResourcesDirectory(string projectId) =>
        EnsureDir(Path.Combine(DataDirectory, "projects", Require(projectId, nameof(projectId)), "resources"));

    public string GetResourceDirectory(string projectId, string resourceId) =>
        EnsureDir(Path.Combine(
            DataDirectory, "projects", Require(projectId, nameof(projectId)),
            "resources", Require(resourceId, nameof(resourceId))));

    public string GetExportsDirectory() => EnsureDir(Path.Combine(DataDirectory, "exports"));

    public string GetLogsDirectory() => EnsureDir(Path.Combine(DataDirectory, "logs"));

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Id must be a non-empty string.", name);
        return value;
    }
}

namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// The single path-resolution/validation layer that confines every built-in tool to the active
/// project's directory tree (<c>projects/{projectId}</c>) (SPEC §7.4). It canonicalizes each
/// requested path and verifies the resolved location is a descendant of the project root, rejecting
/// absolute/rooted paths, <c>..</c> traversal, sibling projects, the SQLite database, and app files
/// with a <see cref="SandboxViolationException"/>. The guard is the security boundary and is
/// deliberately built and tested before the tools so no tool can bypass it.
/// </summary>
public sealed class ProjectSandbox
{
    private readonly string _root;

    /// <summary>Creates a sandbox rooted at the project's directory.</summary>
    /// <param name="projectRoot">The absolute <c>projects/{projectId}</c> directory.</param>
    /// <exception cref="ArgumentException"><paramref name="projectRoot"/> is null or whitespace.</exception>
    public ProjectSandbox(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("Project root must be a non-empty path.", nameof(projectRoot));

        _root = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>The canonical sandbox root every resolved path must stay within.</summary>
    public string Root => _root;

    /// <summary>
    /// Resolves a project-relative path to an absolute path inside the sandbox, or throws when it
    /// escapes. Absolute/rooted paths are rejected outright; relative paths are canonicalized against
    /// the root and verified to be a descendant of it.
    /// </summary>
    /// <param name="requestedPath">A project-relative path a tool wants to reach.</param>
    /// <returns>The absolute, sandbox-contained path.</returns>
    /// <exception cref="SandboxViolationException">The path escapes the sandbox.</exception>
    public string Resolve(string requestedPath)
    {
        if (!TryResolve(requestedPath, out var full))
            throw new SandboxViolationException(requestedPath ?? string.Empty, _root);
        return full;
    }

    /// <summary>
    /// Attempts to resolve a project-relative path inside the sandbox without throwing.
    /// </summary>
    /// <param name="requestedPath">A project-relative path a tool wants to reach.</param>
    /// <param name="fullPath">The absolute, sandbox-contained path when resolution succeeds.</param>
    /// <returns><c>true</c> when the path stays within the sandbox; otherwise <c>false</c>.</returns>
    public bool TryResolve(string? requestedPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(requestedPath))
            return false;

        // An absolute or rooted path (e.g. "/db.sqlite", "C:\Users\...") never targets the sandbox.
        if (Path.IsPathRooted(requestedPath))
            return false;

        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(_root, requestedPath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!IsWithin(combined))
            return false;

        fullPath = combined;
        return true;
    }

    /// <summary>Returns whether an absolute path is the sandbox root or a descendant of it.</summary>
    public bool IsWithin(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        var candidate = Path.GetFullPath(absolutePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return candidate.Equals(_root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

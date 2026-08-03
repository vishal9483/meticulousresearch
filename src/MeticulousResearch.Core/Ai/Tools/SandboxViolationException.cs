namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// Raised when a built-in tool call targets a path that escapes the active project's sandbox
/// (<c>projects/{projectId}</c>) — path traversal (<c>..</c>), an absolute/rooted path, another
/// project's directory, the SQLite database, or any app file (SPEC §7.4). The offending request is
/// rejected before anything outside the sandbox is read or written.
/// </summary>
public sealed class SandboxViolationException : Exception
{
    /// <summary>Creates the exception for a rejected path relative to a project root.</summary>
    /// <param name="requestedPath">The path the tool attempted to reach.</param>
    /// <param name="projectRoot">The sandbox root the path escaped.</param>
    public SandboxViolationException(string requestedPath, string projectRoot)
        : base($"Sandbox violation: '{requestedPath}' resolves outside the project sandbox '{projectRoot}'.")
    {
        RequestedPath = requestedPath;
        ProjectRoot = projectRoot;
    }

    /// <summary>The path the tool attempted to reach.</summary>
    public string RequestedPath { get; }

    /// <summary>The sandbox root the path escaped.</summary>
    public string ProjectRoot { get; }
}

namespace MeticulousResearch.Core.Environment;

/// <summary>
/// Abstraction over environment-variable access so credential/endpoint resolution can be
/// tested deterministically. Nothing in the app reads <c>System.Environment</c> inline —
/// it goes through this interface (settings-secure-key/phase.md).
/// </summary>
public interface IEnvironment
{
    /// <summary>
    /// Returns the value of the named environment variable, or <c>null</c> if it is unset.
    /// An empty string is returned as-is (callers decide whether empty counts as "set").
    /// </summary>
    string? GetEnvironmentVariable(string name);
}

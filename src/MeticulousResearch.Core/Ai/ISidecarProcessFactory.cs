namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Launches sidecar processes (SPEC §7.2). Abstracted so the supervisor's launch/restart/throttle
/// logic is <c>@unit</c>-testable without spawning a real OS process; the production implementation
/// is <see cref="NodeSidecarProcessFactory"/>.
/// </summary>
public interface ISidecarProcessFactory
{
    /// <summary>
    /// Starts a new sidecar bound to a loopback address on an ephemeral port with a fresh
    /// per-session token. Throws when the sidecar cannot be started.
    /// </summary>
    ISidecarProcess Start(SidecarStartInfo startInfo);
}

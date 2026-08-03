namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Production <see cref="ISidecarProcessFactory"/> that would spawn the bundled Node/TypeScript Agent
/// SDK sidecar (SPEC §7.2), bind it to <c>127.0.0.1</c> on an ephemeral port with a per-session
/// token, and connect over the loopback WebSocket.
/// </summary>
/// <remarks>
/// <b>Cross-feature seam.</b> Packaging the Node runtime and the compiled sidecar binary is owned by
/// the <c>installer</c> feature; the sidecar's TypeScript host and its built-in tool loop are owned by
/// <c>builtin-file-tools-sandbox</c>. Until those artifacts are bundled there is no sidecar executable
/// to launch, so <see cref="Start"/> reports the backend as unavailable through the same error channel
/// as any other launch failure. The supervisor and the direct-API fallback behave correctly in the
/// meantime: <see cref="SidecarSupervisor"/> throttles the failing launches, and users on the sidecar
/// preference receive a clear "backend unavailable" message rather than a crash.
/// </remarks>
public sealed class NodeSidecarProcessFactory : ISidecarProcessFactory
{
    /// <inheritdoc />
    public ISidecarProcess Start(SidecarStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        // The bundled sidecar binary is provided by the installer feature; until then there is
        // nothing to launch. Surfacing this as a launch failure keeps the supervisor/back-off and
        // the "backend unavailable" messaging consistent, and lets the direct-API fallback take over.
        throw new SidecarUnavailableException(
            "The Agent SDK sidecar is not available in this build. Switch the backend to the " +
            "direct API in Settings, or reinstall to restore the bundled sidecar.");
    }
}

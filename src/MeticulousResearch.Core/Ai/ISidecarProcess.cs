namespace MeticulousResearch.Core.Ai;

/// <summary>
/// A launched sidecar process (SPEC §7.2). Exposes its loopback <see cref="Endpoint"/>, the launch
/// <see cref="CommandLine"/> (which must never contain the API key), whether it has exited, and a
/// streaming <see cref="Send"/> that delivers the key over the authenticated channel per request.
/// The <see cref="Exited"/> event lets the supervisor auto-restart a crashed sidecar.
/// </summary>
public interface ISidecarProcess : IDisposable
{
    /// <summary>The loopback endpoint (host, ephemeral port, per-session token) the sidecar listens on.</summary>
    SidecarEndpoint Endpoint { get; }

    /// <summary>The command line used to launch the process — never contains the API key.</summary>
    string CommandLine { get; }

    /// <summary>True once the process has exited (crashed or was stopped).</summary>
    bool HasExited { get; }

    /// <summary>Raised when the process exits unexpectedly so the supervisor can restart it.</summary>
    event EventHandler? Exited;

    /// <summary>
    /// True whether a client presenting <paramref name="token"/> would be accepted. Only the
    /// per-session <see cref="SidecarEndpoint.Token"/> is accepted; any other value is refused.
    /// </summary>
    bool AcceptsConnection(string token);

    /// <summary>
    /// Streams the request over the authenticated channel, delivering
    /// <see cref="ChatRequest.ApiKey"/> to the sidecar securely (never via the command line). Throws
    /// <see cref="SidecarCrashedException"/> if the process exits mid-stream.
    /// </summary>
    IAsyncEnumerable<ChatEvent> Send(ChatRequest request, CancellationToken cancellationToken = default);
}

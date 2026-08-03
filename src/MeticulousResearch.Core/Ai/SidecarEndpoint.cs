using System.Net;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The loopback endpoint a launched sidecar listens on (SPEC §7.2): a <c>127.0.0.1</c>/<c>::1</c>
/// host, an ephemeral port assigned at launch, and a per-session authentication token. Clients that
/// do not present <see cref="Token"/> are refused.
/// </summary>
/// <param name="Host">The bound host — always a loopback address.</param>
/// <param name="Port">The ephemeral port assigned at launch.</param>
/// <param name="Token">The per-session authentication token.</param>
public sealed record SidecarEndpoint(string Host, int Port, string Token)
{
    /// <summary>True when <see cref="Host"/> is a loopback address (the only allowed binding).</summary>
    public bool IsLoopback =>
        IPAddress.TryParse(Host, out var ip)
            ? IPAddress.IsLoopback(ip)
            : string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase);
}

using System.Runtime.CompilerServices;
using System.Text;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// In-memory <see cref="ISidecarProcess"/> for sidecar transport/security/restart @unit tests. It
/// exposes a loopback endpoint and per-session token, records the key delivered over the channel, and
/// can be scripted to stream tokens, crash mid-stream, or be launched already-exited.
/// </summary>
internal sealed class FakeSidecarProcess : ISidecarProcess
{
    private readonly List<string> _tokens = new();

    public FakeSidecarProcess(SidecarEndpoint endpoint, string commandLine, bool exitedAtLaunch = false)
    {
        Endpoint = endpoint;
        CommandLine = commandLine;
        HasExited = exitedAtLaunch;
    }

    public SidecarEndpoint Endpoint { get; }
    public string CommandLine { get; }
    public bool HasExited { get; private set; }
    public event EventHandler? Exited;

    public ChatUsage Usage { get; set; } = ChatUsage.Zero;
    public bool CrashAtEnd { get; set; }
    public int CrashAfterTokens { get; set; } = int.MaxValue;

    public string? DeliveredKey { get; private set; }
    public bool KeyDeliveredOverChannel { get; private set; }
    public ChatRequest? LastRequest { get; private set; }

    public FakeSidecarProcess WithTokens(params string[] tokens)
    {
        _tokens.AddRange(tokens);
        return this;
    }

    public FakeSidecarProcess WithUsage(ChatUsage usage)
    {
        Usage = usage;
        return this;
    }

    public bool AcceptsConnection(string token) =>
        !HasExited && string.Equals(token, Endpoint.Token, StringComparison.Ordinal);

    public async IAsyncEnumerable<ChatEvent> Send(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        DeliveredKey = request.ApiKey;
        KeyDeliveredOverChannel = true;

        if (HasExited)
            throw new SidecarCrashedException();

        var text = new StringBuilder();
        var index = 0;
        foreach (var token in _tokens)
        {
            if (index >= CrashAfterTokens)
            {
                SetExited();
                throw new SidecarCrashedException();
            }

            text.Append(token);
            yield return new ChatTokenDelta(token);
            await Task.Yield();
            index++;
        }

        if (CrashAtEnd)
        {
            SetExited();
            throw new SidecarCrashedException();
        }

        yield return new ChatCompleted(text.ToString(), Usage);
    }

    /// <summary>Simulates an unexpected process exit, raising <see cref="Exited"/>.</summary>
    public void SimulateExit() => SetExited();

    private void SetExited()
    {
        if (HasExited)
            return;
        HasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Deterministic <see cref="ISidecarProcessFactory"/>. Each launch yields a distinct endpoint/token,
/// so restart tests can assert a fresh session. Can be configured to crash on launch or throw.
/// </summary>
internal sealed class FakeSidecarProcessFactory : ISidecarProcessFactory
{
    private int _sequence;

    public bool CrashOnLaunch { get; set; }
    public bool ThrowOnLaunch { get; set; }
    public int StartCount { get; private set; }
    public List<FakeSidecarProcess> Created { get; } = new();

    /// <summary>Applied to each created process so tests can script tokens/usage/crash behavior.</summary>
    public Action<FakeSidecarProcess>? Configure { get; set; }

    public ISidecarProcess Start(SidecarStartInfo startInfo)
    {
        StartCount++;
        if (ThrowOnLaunch)
            throw new InvalidOperationException("sidecar failed to start");

        var n = ++_sequence;
        var endpoint = new SidecarEndpoint("127.0.0.1", 49_000 + n, $"session-token-{n}-{Guid.NewGuid():N}");
        var process = new FakeSidecarProcess(
            endpoint,
            $"node sidecar.js --host {endpoint.Host} --port {endpoint.Port} --token {endpoint.Token} --base-url {startInfo.BaseUrl}",
            exitedAtLaunch: CrashOnLaunch);
        Configure?.Invoke(process);
        Created.Add(process);
        return process;
    }
}

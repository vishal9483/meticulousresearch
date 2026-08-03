using MeticulousResearch.Core.Ai;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>Builds each real backend wired to deterministic fakes for cross-backend scenarios.</summary>
internal static class BackendFixtures
{
    public const string Sidecar = "sidecar";
    public const string DirectApi = "direct-api";

    /// <summary>
    /// Builds the named backend scripted to emit <paramref name="tokens"/> then complete with
    /// <paramref name="usage"/>. Both paths resolve credentials via the shared credential provider.
    /// </summary>
    public static IChatService Build(
        string backend,
        ChatUsage usage,
        string[] tokens,
        string storedKey = "sk-stored",
        string? envKey = null,
        string? envBaseUrl = null,
        string? settingBaseUrl = null)
    {
        var creds = AiTestHelpers.Credentials(
            envKey: envKey, storedKey: storedKey, envBaseUrl: envBaseUrl, settingBaseUrl: settingBaseUrl);
        var assembler = new ChatRequestAssembler();

        return backend switch
        {
            Sidecar => new SidecarChatService(creds, assembler, new SidecarSupervisor(
                new FakeSidecarProcessFactory { Configure = p => p.WithTokens(tokens).WithUsage(usage) },
                new FakeClock())),
            DirectApi => new DirectApiChatService(creds, assembler,
                new RecordingDirectApiTransport().ScriptTokensThenComplete(usage, tokens)),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "unknown backend"),
        };
    }
}

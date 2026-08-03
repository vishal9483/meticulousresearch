using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Sidecar transport &amp; security scenarios (SPEC §7.2, §7.5): loopback + ephemeral port, per-session
/// token, and the API key delivered over the authenticated channel — never on the command line.
/// </summary>
public sealed class SidecarTransportSecurityTests
{
    private static (SidecarChatService Service, FakeSidecarProcessFactory Factory) NewSidecar(
        ApiCredentialProvider credentials)
    {
        var factory = new FakeSidecarProcessFactory { Configure = p => p.WithTokens("ok") };
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        return (new SidecarChatService(credentials, new ChatRequestAssembler(), supervisor), factory);
    }

    // @unit @integration
    // Scenario: The sidecar listens on loopback with an ephemeral port
    [Fact]
    [Trait("integration", "true")]
    public void Sidecar_listens_on_loopback_with_ephemeral_port()
    {
        var factory = new FakeSidecarProcessFactory();
        var supervisor = new SidecarSupervisor(factory, new FakeClock());

        var process = supervisor.EnsureRunning(new SidecarStartInfo("https://api.anthropic.com"));

        Assert.True(process.Endpoint.IsLoopback, "sidecar must bind a loopback address");
        Assert.True(process.Endpoint.Port > 0, "an ephemeral port must be assigned at launch");
    }

    // @unit @integration
    // Scenario: The gateway authenticates to the sidecar with a per-session token
    [Fact]
    [Trait("integration", "true")]
    public void Sidecar_refuses_without_token_and_accepts_with_correct_token()
    {
        var factory = new FakeSidecarProcessFactory();
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        var process = supervisor.EnsureRunning(new SidecarStartInfo("https://api.anthropic.com"));

        Assert.False(process.AcceptsConnection("wrong-token"));
        Assert.False(process.AcceptsConnection(string.Empty));
        Assert.True(process.AcceptsConnection(process.Endpoint.Token));
    }

    // @unit @integration
    // Scenario: The API key is passed over the secure channel, never on the command line
    [Fact]
    [Trait("integration", "true")]
    public async Task Key_is_delivered_over_channel_from_secure_store_not_command_line()
    {
        var creds = AiTestHelpers.Credentials(storedKey: "sk-secret-store");
        var (service, factory) = NewSidecar(creds);

        await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));

        var process = Assert.Single(factory.Created);
        // Never on the command line.
        Assert.DoesNotContain("sk-secret-store", process.CommandLine, StringComparison.Ordinal);
        // Delivered over the authenticated channel.
        Assert.True(process.KeyDeliveredOverChannel);
        Assert.Equal("sk-secret-store", process.DeliveredKey);
    }

    // @unit
    // Scenario: Missing API key produces a clear, actionable error before any request
    [Fact]
    public async Task Missing_api_key_produces_actionable_error_before_any_request()
    {
        var creds = AiTestHelpers.Credentials(); // no env key, no stored key
        var transport = new RecordingDirectApiTransport().ScriptTokensThenComplete(ChatUsage.Zero, "ok");
        var service = new DirectApiChatService(creds, new ChatRequestAssembler(), transport);

        var events = await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));

        var fault = Assert.IsType<ChatFaulted>(Assert.Single(events));
        Assert.Equal(ChatErrorKind.MissingApiKey, fault.Kind);
        // Human-readable, mentions the key and points at Settings.
        Assert.Contains("API key", fault.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Settings", fault.Message, StringComparison.OrdinalIgnoreCase);
        // No raw stack trace.
        Assert.DoesNotContain("   at ", fault.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", fault.Message, StringComparison.Ordinal);
        // No request was made.
        Assert.Equal(0, transport.SendCount);
    }
}

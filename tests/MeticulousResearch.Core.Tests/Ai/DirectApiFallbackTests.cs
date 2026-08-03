using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Tests.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Direct-API fallback scenarios (SPEC §7.2): the direct-API backend implements the same contract end
/// to end without launching a sidecar; the real round trip is an acceptance-only test.
/// </summary>
public sealed class DirectApiFallbackTests
{
    // @unit
    // Scenario: The direct-API backend implements the same contract end to end
    [Fact]
    public async Task Direct_api_streams_completes_and_reports_usage_without_a_sidecar()
    {
        var usage = new ChatUsage(120, 40, 10, 5);
        var creds = AiTestHelpers.Credentials(storedKey: "sk-stored");
        var assembler = new ChatRequestAssembler();

        var transport = new RecordingDirectApiTransport()
            .ScriptTokensThenComplete(usage, "Hello", " world");
        var directService = new DirectApiChatService(creds, assembler, transport);

        // A sidecar backend is available but must never be launched when direct-api is active.
        var sidecarFactory = new FakeSidecarProcessFactory { ThrowOnLaunch = true };
        var sidecarService = new SidecarChatService(
            creds, assembler, new SidecarSupervisor(sidecarFactory, new FakeClock()));

        var backendFactory = new ChatBackendFactory(
            new StubSettings { ChatBackend = "direct-api" }, () => sidecarService, () => directService);

        var events = await AiTestHelpers.Collect(backendFactory.Resolve().Ask(AiTestHelpers.Context()));

        // Streamed tokens, a completion, and usage fields.
        Assert.Equal(new[] { "Hello", " world" }, events.OfType<ChatTokenDelta>().Select(d => d.Text));
        var completion = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal("Hello world", completion.Text);
        Assert.Equal(usage, completion.Usage);

        // No sidecar process is launched.
        Assert.Equal(0, sidecarFactory.StartCount);
    }

    // @requires-network @requires-key
    // Scenario: The direct-API backend performs a real round trip (acceptance only)
    [Fact(Skip = "requires-network + requires-key: touches the real Anthropic API; run manually as acceptance")]
    [Trait("requires-network", "true")]
    [Trait("requires-key", "true")]
    public async Task Direct_api_real_round_trip()
    {
        // Acceptance-only: with a valid API key and the direct-api backend, ask
        // "Reply with the single word: ok" and expect a streamed response plus usage
        // with non-zero input and output tokens. Executed manually; skipped in the gate.
        var http = new System.Net.Http.HttpClient();
        var transport = new HttpDirectApiTransport(http);
        var creds = AiTestHelpers.Credentials(storedKey: System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "");
        var service = new DirectApiChatService(creds, new ChatRequestAssembler(), transport);

        var events = await AiTestHelpers.Collect(
            service.Ask(AiTestHelpers.Context(model: "claude-sonnet-5", message: "Reply with the single word: ok")));

        Assert.Contains(events, e => e is ChatTokenDelta);
        var completion = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.True(completion.Usage.InputTokens > 0);
        Assert.True(completion.Usage.OutputTokens > 0);
    }
}

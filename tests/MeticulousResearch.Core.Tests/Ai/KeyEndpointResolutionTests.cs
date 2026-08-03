using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Key &amp; endpoint resolution scenarios (SPEC §7.5): the env key wins over the stored key, the env
/// key is delivered over the channel (never the command line), both backends target the resolved base
/// URL, the env base URL overrides the setting, and the default public API is used when nothing is set.
/// </summary>
public sealed class KeyEndpointResolutionTests
{
    /// <summary>Runs a question through the named backend and returns the request the backend sent.</summary>
    private static async Task<ChatRequest> SentRequest(string backend, ApiCredentialProvider credentials)
    {
        var assembler = new ChatRequestAssembler();
        if (backend == BackendFixtures.DirectApi)
        {
            var transport = new RecordingDirectApiTransport().ScriptTokensThenComplete(ChatUsage.Zero, "ok");
            var service = new DirectApiChatService(credentials, assembler, transport);
            await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));
            return transport.LastRequest!;
        }

        var factory = new FakeSidecarProcessFactory { Configure = p => p.WithTokens("ok") };
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        var sidecar = new SidecarChatService(credentials, assembler, supervisor);
        await AiTestHelpers.Collect(sidecar.Ask(AiTestHelpers.Context()));
        return factory.Created.Single().LastRequest!;
    }

    // @unit
    // Scenario: The gateway uses the API key from the environment when present
    [Fact]
    public async Task Env_key_is_used_when_present()
    {
        var creds = AiTestHelpers.Credentials(envKey: "sk-from-env", storedKey: "sk-stored");
        var request = await SentRequest(BackendFixtures.DirectApi, creds);
        Assert.Equal("sk-from-env", request.ApiKey);
    }

    // @unit
    // Scenario: The gateway falls back to the stored key when the environment has none
    [Fact]
    public async Task Stored_key_is_used_when_env_absent()
    {
        var creds = AiTestHelpers.Credentials(storedKey: "sk-stored");
        var request = await SentRequest(BackendFixtures.DirectApi, creds);
        Assert.Equal("sk-stored", request.ApiKey);
    }

    // @unit @integration
    // Scenario: The environment key is delivered over the secure channel, never on the command line
    [Fact]
    [Trait("integration", "true")]
    public async Task Env_key_delivered_over_channel_not_command_line()
    {
        var creds = AiTestHelpers.Credentials(envKey: "sk-from-env");
        var factory = new FakeSidecarProcessFactory { Configure = p => p.WithTokens("ok") };
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        var sidecar = new SidecarChatService(creds, new ChatRequestAssembler(), supervisor);

        await AiTestHelpers.Collect(sidecar.Ask(AiTestHelpers.Context()));

        var process = factory.Created.Single();
        Assert.DoesNotContain("sk-from-env", process.CommandLine, StringComparison.Ordinal);
        Assert.True(process.KeyDeliveredOverChannel);
        Assert.Equal("sk-from-env", process.DeliveredKey);
    }

    // @unit
    // Scenario Outline: Both backends target the resolved base URL, never a hardcoded endpoint
    [Theory]
    [InlineData(BackendFixtures.Sidecar)]
    [InlineData(BackendFixtures.DirectApi)]
    public async Task Both_backends_target_resolved_base_url(string backend)
    {
        var creds = AiTestHelpers.Credentials(storedKey: "sk-stored", settingBaseUrl: "https://llm.sdc.siemens.cloud");
        var request = await SentRequest(backend, creds);
        Assert.Equal("https://llm.sdc.siemens.cloud", request.BaseUrl);
    }

    // @unit
    // Scenario: The base URL from the environment overrides the persisted setting
    [Fact]
    public async Task Env_base_url_overrides_setting()
    {
        var creds = AiTestHelpers.Credentials(
            storedKey: "sk-stored",
            envBaseUrl: "https://llm.sdc.siemens.cloud",
            settingBaseUrl: "https://llm.example.internal");

        var request = await SentRequest(BackendFixtures.DirectApi, creds);

        Assert.Equal("https://llm.sdc.siemens.cloud", request.BaseUrl);
    }

    // @unit
    // Scenario: With no endpoint configured the gateway uses the default public Anthropic API
    [Fact]
    public async Task Default_public_api_when_nothing_configured()
    {
        var creds = AiTestHelpers.Credentials(storedKey: "sk-stored"); // no env, no setting
        var request = await SentRequest(BackendFixtures.DirectApi, creds);
        Assert.Equal(AnthropicApi.DefaultBaseUrl, request.BaseUrl);
    }
}

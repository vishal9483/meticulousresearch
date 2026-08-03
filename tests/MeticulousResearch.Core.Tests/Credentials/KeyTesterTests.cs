using System.Net;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Tests.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Credentials;

/// <summary>
/// @unit scenarios for "Test key" (docs/features/settings-secure-key/tests.md — "Test key").
/// The network is mocked; the tester must call the RESOLVED base URL, never a hardcoded endpoint.
/// </summary>
public sealed class KeyTesterTests
{
    private const string ModelsJson =
        "{\"data\":[{\"id\":\"claude-opus-5\"},{\"id\":\"claude-sonnet-4\"}]}";

    // @unit @requires-key
    // Scenario: Testing a valid key reports success and lists models
    [Fact]
    [Trait("Category", "requires-key")]
    public async Task Testing_a_valid_key_reports_success_and_lists_models()
    {
        var creds = ProviderWithStoredKey(baseUrlEnv: null, settingBaseUrl: null);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ModelsJson),
        });
        var tester = new KeyTester(creds, new HttpClient(handler));

        var result = await tester.TestAsync();

        Assert.True(result.Success);
        Assert.Contains("claude-opus-5", result.Models);
        Assert.Contains("claude-sonnet-4", result.Models);
    }

    // @unit
    // Scenario: Test key calls the resolved base URL, not a hardcoded endpoint
    [Fact]
    public async Task Test_key_calls_the_resolved_base_url()
    {
        var creds = ProviderWithStoredKey(baseUrlEnv: "https://llm.sdc.siemens.cloud", settingBaseUrl: null);
        Assert.Equal("https://llm.sdc.siemens.cloud", creds.ResolveBaseUrl());

        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ModelsJson),
        });
        var tester = new KeyTester(creds, new HttpClient(handler));

        var result = await tester.TestAsync();

        Assert.NotNull(handler.LastRequestUri);
        Assert.StartsWith("https://llm.sdc.siemens.cloud", handler.LastRequestUri!.ToString());
        Assert.True(result.Success);
    }

    // @unit
    // Scenario: Testing an invalid key reports a clear, actionable error
    [Fact]
    public async Task Testing_an_invalid_key_reports_a_clear_actionable_error()
    {
        var creds = ProviderWithStoredKey(baseUrlEnv: null, settingBaseUrl: null);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var tester = new KeyTester(creds, new HttpClient(handler));

        var result = await tester.TestAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        // no raw stack trace is shown
        Assert.DoesNotContain("   at ", result.ErrorMessage!);
        Assert.DoesNotContain("Exception", result.ErrorMessage!);
    }

    private static ApiCredentialProvider ProviderWithStoredKey(string? baseUrlEnv, string? settingBaseUrl)
    {
        var keyStore = new FakeSecureKeyStore();
        keyStore.Save("sk-stored");
        var env = new FakeEnvironment();
        if (baseUrlEnv is not null)
            env.Set("ANTHROPIC_BASE_URL", baseUrlEnv);
        var settings = new StubSettings { ApiBaseUrl = settingBaseUrl };
        return new ApiCredentialProvider(env, keyStore, settings);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response) => _response = response;

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_response);
        }
    }
}

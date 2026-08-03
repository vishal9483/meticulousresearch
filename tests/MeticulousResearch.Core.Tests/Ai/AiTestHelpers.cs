using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>Shared helpers for AI-gateway tests.</summary>
internal static class AiTestHelpers
{
    public static async Task<List<ChatEvent>> Collect(
        IAsyncEnumerable<ChatEvent> stream, CancellationToken cancellationToken = default)
    {
        var events = new List<ChatEvent>();
        await foreach (var e in stream.WithCancellation(cancellationToken))
            events.Add(e);
        return events;
    }

    public static ChatAskContext Context(
        string model = "claude-opus-5",
        string message = "hello",
        string? customInstructions = null,
        IReadOnlyList<ChatResource>? resources = null,
        IReadOnlyList<ChatHistoryMessage>? history = null) => new()
        {
            Model = model,
            UserMessage = message,
            CustomInstructions = customInstructions,
            Resources = resources ?? Array.Empty<ChatResource>(),
            History = history ?? Array.Empty<ChatHistoryMessage>(),
        };

    /// <summary>Builds a real credential provider over fakes so env-wins resolution is faithful.</summary>
    public static ApiCredentialProvider Credentials(
        string? envKey = null,
        string? storedKey = null,
        string? envBaseUrl = null,
        string? settingBaseUrl = null)
    {
        var env = new FakeEnvironment();
        if (envKey is not null) env.Set(AnthropicApi.ApiKeyEnvVar, envKey);
        if (envBaseUrl is not null) env.Set(AnthropicApi.BaseUrlEnvVar, envBaseUrl);

        var keyStore = new FakeSecureKeyStore();
        if (storedKey is not null) keyStore.Save(storedKey);

        var settings = new StubSettings { ApiBaseUrl = settingBaseUrl };
        return new ApiCredentialProvider(env, keyStore, settings);
    }
}

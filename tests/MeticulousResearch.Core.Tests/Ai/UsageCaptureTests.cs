using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Usage-capture scenarios (SPEC §3.6): both backends surface the API's usage fields identically.
/// </summary>
public sealed class UsageCaptureTests
{
    // @unit
    // Scenario Outline: Usage fields are surfaced identically regardless of backend
    [Theory]
    [InlineData(BackendFixtures.Sidecar)]
    [InlineData(BackendFixtures.DirectApi)]
    public async Task Usage_is_surfaced_identically(string backend)
    {
        var usage = new ChatUsage(InputTokens: 1200, OutputTokens: 350, CacheReadTokens: 800, CacheWriteTokens: 200);
        var service = BackendFixtures.Build(backend, usage, new[] { "answer" });

        var events = await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));

        var completion = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal(1200, completion.Usage.InputTokens);
        Assert.Equal(350, completion.Usage.OutputTokens);
        Assert.Equal(800, completion.Usage.CacheReadTokens);
        Assert.Equal(200, completion.Usage.CacheWriteTokens);
    }
}

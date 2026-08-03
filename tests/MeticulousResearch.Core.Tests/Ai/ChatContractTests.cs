using MeticulousResearch.Core.Ai;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// The backend-agnostic <see cref="IChatService"/> contract, pinned via <see cref="FakeChatService"/>
/// (docs/features/ai-gateway/tests.md — "The IChatService contract").
/// </summary>
public sealed class ChatContractTests
{
    // @unit
    // Scenario: A chat request yields a stream of tokens then a completion with usage
    [Fact]
    public async Task Yields_tokens_in_order_then_completion_with_usage()
    {
        var fake = new FakeChatService()
            .WithTokens("Hello", " ", "world")
            .WithUsage(input: 10, output: 3);

        var events = await AiTestHelpers.Collect(fake.Ask(AiTestHelpers.Context()));

        var deltas = events.OfType<ChatTokenDelta>().Select(d => d.Text).ToArray();
        Assert.Equal(new[] { "Hello", " ", "world" }, deltas);

        var completion = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal("Hello world", completion.Text);
        Assert.Equal(10, completion.Usage.InputTokens);
        Assert.Equal(3, completion.Usage.OutputTokens);
    }

    // @unit
    // Scenario: Cancelling a request stops the stream promptly
    [Fact]
    public async Task Cancelling_stops_stream_and_completes_cancelled()
    {
        var fake = new FakeChatService().WithTokens("Hello", " ", "world");
        using var cts = new CancellationTokenSource();

        var events = new List<ChatEvent>();
        await foreach (var e in fake.Ask(AiTestHelpers.Context(), cts.Token))
        {
            events.Add(e);
            if (e is ChatTokenDelta)
                cts.Cancel(); // cancel after the first token arrives
        }

        // No further tokens delivered after cancellation (only the first "Hello").
        var deltas = events.OfType<ChatTokenDelta>().Select(d => d.Text).ToArray();
        Assert.Equal(new[] { "Hello" }, deltas);

        // The request completes in a cancelled state — no ChatCompleted.
        Assert.IsType<ChatCancelled>(events[^1]);
        Assert.DoesNotContain(events, e => e is ChatCompleted);
    }

    // @unit
    // Scenario: Missing cache fields default to zero, not error
    [Fact]
    public async Task Missing_cache_fields_default_to_zero()
    {
        var fake = new FakeChatService()
            .WithTokens("ok")
            .WithUsage(new ChatUsage(InputTokens: 100, OutputTokens: 20)); // cache fields omitted

        var events = await AiTestHelpers.Collect(fake.Ask(AiTestHelpers.Context()));

        var completion = Assert.IsType<ChatCompleted>(events[^1]);
        Assert.Equal(0, completion.Usage.CacheReadTokens);
        Assert.Equal(0, completion.Usage.CacheWriteTokens);
    }
}

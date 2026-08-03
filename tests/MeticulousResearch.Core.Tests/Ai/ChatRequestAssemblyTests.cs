using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Request-assembly scenarios (SPEC §7.3): the payload the backend receives is built from custom
/// instructions + in-scope resources + history + message, and carries the selected model. Exercised
/// through <see cref="DirectApiChatService"/> with a recording transport so the assertions inspect
/// exactly what the backend would send.
/// </summary>
public sealed class ChatRequestAssemblyTests
{
    // @unit
    // Scenario: The request is assembled from custom instructions, resources, history, and message
    [Fact]
    public async Task Request_contains_instructions_resources_history_and_message()
    {
        var transport = new RecordingDirectApiTransport()
            .ScriptTokensThenComplete(ChatUsage.Zero, "ok");
        var service = new DirectApiChatService(
            AiTestHelpers.Credentials(storedKey: "sk-stored"), new ChatRequestAssembler(), transport);

        var context = AiTestHelpers.Context(
            model: "claude-opus-5",
            message: "What is the market size?",
            customInstructions: "Cite sources",
            resources: new[]
            {
                new ChatResource("r1", "Filing", "Revenue was $1B"),
                new ChatResource("r2", "Interview", "The CEO said growth is strong"),
            },
            history: new[] { new ChatHistoryMessage("user", "Earlier question") });

        await AiTestHelpers.Collect(service.Ask(context));

        var request = Assert.IsType<ChatRequest>(transport.LastRequest);
        // Custom instructions as system context.
        Assert.Equal("Cite sources", request.System);
        // The two in-scope resources.
        Assert.Equal(2, request.Resources.Count);
        Assert.Collection(request.Resources,
            r => Assert.Equal("r1", r.Id),
            r => Assert.Equal("r2", r.Id));
        // The prior turn.
        var priorTurn = Assert.Single(request.History);
        Assert.Equal("Earlier question", priorTurn.Content);
        // The user message.
        Assert.Equal("What is the market size?", request.UserMessage);
    }

    // @unit
    // Scenario: The selected model is forwarded to the backend
    [Fact]
    public async Task Selected_model_is_forwarded()
    {
        var transport = new RecordingDirectApiTransport()
            .ScriptTokensThenComplete(ChatUsage.Zero, "ok");
        var service = new DirectApiChatService(
            AiTestHelpers.Credentials(storedKey: "sk-stored"), new ChatRequestAssembler(), transport);

        await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context(model: "claude-sonnet-5")));

        Assert.Equal("claude-sonnet-5", transport.LastRequest!.Model);
    }
}

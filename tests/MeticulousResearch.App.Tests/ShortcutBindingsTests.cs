using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit scenarios from docs/features/command-palette-shortcuts/tests.md for the global
/// send/stop keyboard shortcuts, exercised at the conversation view-model layer (the shortcut
/// bindings route Ctrl+Enter → Send and Esc → Stop). Window-free.
/// </summary>
public class ShortcutBindingsTests
{
    // Scenario: Ctrl+Enter in the composer sends the message
    //   Given the conversation composer has text
    //   When I trigger the send shortcut
    //   Then the message is sent
    [Fact]
    public async Task Ctrl_Enter_in_the_composer_sends_the_message()
    {
        var conversations = new FakeConversationService();
        var vm = new ConversationsViewModel("P1", conversations, settings: null);
        vm.Draft = "How did the market do?";

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, conversations.AskCount);
        Assert.Equal("How did the market do?", conversations.LastAsked);
    }

    // Scenario: Esc during a streaming generation stops it
    //   Given a generation is streaming
    //   When I trigger the stop shortcut
    //   Then the generation is cancelled
    [Fact]
    public async Task Esc_during_a_streaming_generation_stops_it()
    {
        var conversations = new FakeConversationService();
        var streaming = new BlockingStreamingService();
        var vm = new ConversationsViewModel("P1", conversations, settings: null, catalog: null, streaming: streaming);
        vm.Draft = "stream this";

        var send = vm.SendCommand.ExecuteAsync(null);
        await streaming.Started.Task; // generation is now streaming

        Assert.True(vm.StopCommand.CanExecute(null));
        vm.StopCommand.Execute(null); // trigger the stop shortcut

        await send;
        Assert.True(streaming.WasCancelled);
    }

    // Scenario: Esc does nothing destructive when nothing is streaming
    //   Given no generation is in progress
    //   When I trigger the stop shortcut
    //   Then no action is taken and no error is shown
    [Fact]
    public void Esc_does_nothing_destructive_when_nothing_is_streaming()
    {
        var vm = new ConversationsViewModel("P1", new FakeConversationService(), settings: null);

        Assert.False(vm.StopCommand.CanExecute(null));

        var ex = Record.Exception(() => vm.StopCommand.Execute(null));
        Assert.Null(ex); // no action taken, no error shown
    }

    private sealed class FakeConversationService : IConversationService
    {
        public int AskCount { get; private set; }
        public string? LastAsked { get; private set; }

        public Conversation Create(string projectId) =>
            new() { Id = "C1", ProjectId = projectId };

        public IReadOnlyList<Conversation> List(string projectId) => Array.Empty<Conversation>();
        public Conversation? Get(string conversationId) => null;
        public void Delete(string conversationId) { }
        public IReadOnlyList<Message> GetMessages(string conversationId) => Array.Empty<Message>();

        public Task<Message> Ask(
            string conversationId,
            string message,
            string model,
            IReadOnlyList<ChatResource>? resourceScope = null,
            CancellationToken cancellationToken = default)
        {
            AskCount++;
            LastAsked = message;
            return Task.FromResult(new Message
            {
                Id = "M1",
                ConversationId = conversationId,
                Role = "assistant",
                Content = "reply",
                Model = model,
            });
        }

        public Task<Message> Ask(
            string conversationId,
            string message,
            string model,
            IReadOnlyList<ChatResource>? resourceScope,
            IReadOnlyList<ImageAttachment>? attachments,
            CancellationToken cancellationToken = default)
            => Ask(conversationId, message, model, resourceScope, cancellationToken);
    }

    private sealed class BlockingStreamingService : IStreamingConversationService
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasCancelled { get; private set; }

        public async Task<StreamingTurn> StreamAsk(
            string conversationId,
            string message,
            string model,
            Action<StreamingTurn>? onDelta = null,
            IReadOnlyList<ChatResource>? resourceScope = null,
            CancellationToken cancellationToken = default)
        {
            var turn = new StreamingTurn(conversationId, model);
            Started.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
            }
            return turn;
        }

        public Task<StreamingTurn> Resume(
            StreamingTurn turn,
            Action<StreamingTurn>? onDelta = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(turn);
    }
}

using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/conversations/tests.md. These drive the real WPF window via
/// FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and excluded
/// from the headless gate; they must compile and build. They reuse the shell fixture and open a
/// project workspace before exercising the Conversations section.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ConversationsUiTests
{
    private readonly ShellUiFixture _fixture;

    public ConversationsUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Sending a message shows both the user turn and the assistant reply in the thread
    //   Given a conversation is open
    //   When I type "Hello" and send
    //   Then my message appears in the thread
    //   And the assistant reply appears below it
    [Fact]
    public void Sending_a_message_shows_both_the_user_turn_and_the_assistant_reply_in_the_thread()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        // type "Hello" and send
        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Hello";

        var sendButton = conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton();
        Assert.NotNull(sendButton);
        sendButton!.Click();

        // the thread hosts the turns
        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);

        // my message appears in the thread
        var userTurn = thread!.FindFirstDescendant(cf => cf.ByName("Hello"));
        Assert.NotNull(userTurn);

        // and the assistant reply appears below it (a second turn is rendered after the user's)
        var turns = thread.FindAllChildren();
        Assert.True(turns.Length >= 2, "expected both a user turn and an assistant reply in the thread");
    }

    // Scenario: An empty conversation shows a designed empty state
    //   Given a new conversation with no messages
    //   When I open it
    //   Then I see an empty state prompting me to ask my first question
    [Fact]
    public void An_empty_conversation_shows_a_designed_empty_state()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        // I see an empty state prompting me to ask my first question
        var emptyState = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationEmptyState"));
        Assert.NotNull(emptyState);
        var prompt = emptyState!.FindFirstDescendant(cf => cf.ByName("Ask your first question to start a grounded conversation."));
        Assert.NotNull(prompt);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Conversations section, returning the center
    /// pane content. Opening a project reuses the projects-crud open affordance (available on the
    /// base integration branch); this fails loudly if that seam is missing so the test is never
    /// silently green.
    /// </summary>
    private static AutomationElement OpenConversationsView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Conversations"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

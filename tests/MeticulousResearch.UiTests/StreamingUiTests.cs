using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/streaming/tests.md (SPEC §3.3 incremental render + Stop; §8
/// interrupted-turn resume affordance). These drive the real WPF window via FlaUI (UIA3) and
/// require a desktop session, so they are tagged <c>Category=ui</c> and excluded from the headless
/// gate; they must compile and build. They reuse the shell fixture and open a project workspace
/// before exercising the Conversations section's streaming wiring.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class StreamingUiTests
{
    private readonly ShellUiFixture _fixture;

    public StreamingUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The reply renders incrementally in the thread
    //   Given a conversation is open
    //   When I send a message and the backend streams a reply
    //   Then I see text appear progressively rather than all at once
    [Fact]
    public void The_reply_renders_incrementally_in_the_thread()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Stream please";

        var sendButton = conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton();
        Assert.NotNull(sendButton);
        sendButton!.Click();

        // The thread hosts the streaming assistant turn; a streaming indicator proves the reply is
        // rendered progressively (a live cursor) rather than appearing all at once.
        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);
        var indicator = thread!.FindFirstDescendant(cf => cf.ByAutomationId("StreamingIndicator"));
        Assert.NotNull(indicator);
    }

    // Scenario: Esc / Stop cancels an in-progress generation
    //   Given a response is streaming
    //   When I press Stop
    //   Then streaming ends
    //   And the partial answer remains visible in the thread
    [Fact]
    public void Esc_or_Stop_cancels_an_in_progress_generation()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Long answer";
        conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton()!.Click();

        // When I press Stop (shown only while streaming)
        var stopButton = conversations.FindFirstDescendant(cf => cf.ByAutomationId("StopButton"))?.AsButton();
        Assert.NotNull(stopButton);
        stopButton!.Click();

        // Then streaming ends (the Stop control is no longer offered) and the partial answer remains
        // visible as a rendered turn in the thread.
        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);
        Assert.True(thread!.FindAllChildren().Length >= 1, "the partial answer should remain in the thread");
    }

    // Scenario: An interrupted turn offers a "resume"/"retry" affordance
    //   Given an assistant turn was interrupted mid-stream
    //   Then the turn shows it was interrupted
    //   And offers an action to continue the generation
    [Fact]
    public void An_interrupted_turn_offers_a_resume_affordance()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Interrupt me";
        conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton()!.Click();
        conversations.FindFirstDescendant(cf => cf.ByAutomationId("StopButton"))?.AsButton()?.Click();

        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);

        // The turn shows it was interrupted
        var badge = thread!.FindFirstDescendant(cf => cf.ByAutomationId("InterruptedBadge"));
        Assert.NotNull(badge);

        // And offers an action to continue the generation
        var resume = thread.FindFirstDescendant(cf => cf.ByAutomationId("ResumeButton"))?.AsButton();
        Assert.NotNull(resume);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Conversations section, returning the center
    /// pane content. Fails loudly if the projects-crud open seam is missing so the test is never
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

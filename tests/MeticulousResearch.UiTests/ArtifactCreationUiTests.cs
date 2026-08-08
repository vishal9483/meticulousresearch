using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/artifact-creation/tests.md (SPEC §3.4). These drive the real WPF
/// window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and
/// excluded from the headless gate; they must compile and build. They reuse the shell fixture, open
/// a project workspace, and exercise the Artifacts section's empty state / New-artifact flow and the
/// conversation turn's promote-to-artifact action.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ArtifactCreationUiTests
{
    private readonly ShellUiFixture _fixture;

    public ArtifactCreationUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The Artifacts view shows a designed empty state
    //   Given a project with no artifacts
    //   When I open the project's Artifacts view
    //   Then I see an empty state with a "New artifact" call to action
    [Fact]
    public void The_Artifacts_view_shows_a_designed_empty_state()
    {
        // A project with no artifacts: a fresh empty project, not the populated sample.
        var artifacts = OpenEmptyArtifactsView(_fixture.MainWindow);

        // I see an empty state
        var emptyState = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("ArtifactsEmptyState"));
        Assert.NotNull(emptyState);

        // with a "New artifact" call to action
        var newButton = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("NewArtifactButton"))?.AsButton();
        Assert.NotNull(newButton);
    }

    // Scenario: New artifact opens the artifact editor on success
    //   Given the "New artifact" flow is open
    //   When I generate an artifact directly
    //   Then the artifact editor for the new artifact is shown
    [Fact]
    public void New_artifact_opens_the_artifact_editor_on_success()
    {
        // Use a throwaway empty project so creating an artifact never pollutes the shared sample.
        var artifacts = OpenEmptyArtifactsView(_fixture.MainWindow);

        // When I create/generate a new artifact via the flow
        var newButton = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("NewArtifactButton"))?.AsButton();
        Assert.NotNull(newButton);
        newButton!.Click();

        // Then the artifact editor destination (the selectable artifact list) is shown with the new artifact
        var list = FlaUI.Core.Tools.Retry.WhileNull(
            () => artifacts.FindFirstDescendant(cf => cf.ByAutomationId("ArtifactsList")),
            System.TimeSpan.FromSeconds(10)).Result;
        Assert.NotNull(list);
    }

    // Scenario: Promote-to-artifact is offered on an assistant turn
    //   Given a conversation with an assistant turn
    //   When I open the turn's actions
    //   Then a "Promote to artifact" action is available
    [Fact]
    public void Promote_to_artifact_is_offered_on_an_assistant_turn()
    {
        var conversations = OpenConversationsView(_fixture.MainWindow);

        // Given a conversation with an assistant turn: send a message (the @ui fake completes it).
        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "What is the market size?";
        var send = conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton();
        Assert.NotNull(send);
        send!.Click();

        var thread = FlaUI.Core.Tools.Retry.WhileNull(
            () => conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread")),
            System.TimeSpan.FromSeconds(10)).Result;
        Assert.NotNull(thread);

        // When I open the completed turn's action menu.
        var actions = FlaUI.Core.Tools.Retry.WhileNull(
            () => thread!.FindFirstDescendant(cf => cf.ByAutomationId("TurnActionMenu")),
            System.TimeSpan.FromSeconds(10)).Result;
        Assert.NotNull(actions);
        var menu = actions!.FindFirstDescendant(cf => cf.ByName("Actions"))?.AsMenuItem();
        Assert.NotNull(menu);
        menu!.Click();

        // A "Promote to artifact" action is available.
        var promote = thread!.FindFirstDescendant(cf => cf.ByName("Promote to artifact"))
                      ?? thread.FindFirstDescendant(cf => cf.ByAutomationId("PromoteAction"));
        Assert.NotNull(promote);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Artifacts section, returning the center pane
    /// content. Fails loudly if the project-open seam (projects-crud) is missing so the test is
    /// never silently green.
    /// </summary>
    private static AutomationElement OpenArtifactsView(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Artifacts"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }

    private static AutomationElement OpenConversationsView(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Conversations"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }

    /// <summary>
    /// Creates and opens a fresh empty project, then switches to its Artifacts section — used by the
    /// empty-state scenario (a project with no artifacts).
    /// </summary>
    private static AutomationElement OpenEmptyArtifactsView(Window window)
    {
        var workspace = ShellUiFlow.OpenEmptyProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Artifacts"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

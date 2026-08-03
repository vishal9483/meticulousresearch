using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/model-selector/tests.md (SPEC §6, §3.3). These drive the real
/// WPF window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c>
/// and excluded from the headless gate; they must compile and build. They reuse the shell fixture and
/// open the Conversations section, where the model picker and per-turn model label live.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ModelSelectorUiTests
{
    private readonly ShellUiFixture _fixture;

    public ModelSelectorUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The model picker shows friendly tiers with an "All models" expander
    //   Given a conversation is open
    //   When I open the model picker
    //   Then I see the tiers "Frontier", "Deep", "Balanced", "Fast"
    //   And an "All models" section listing the additional models
    [Fact]
    public void The_model_picker_shows_friendly_tiers_with_an_all_models_expander()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        // When I open the model picker
        var picker = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ModelPicker"));
        Assert.NotNull(picker);

        // Then I see the tiers "Frontier", "Deep", "Balanced", "Fast"
        foreach (var tier in new[] { "Frontier", "Deep", "Balanced", "Fast" })
        {
            var tierElement = picker!.FindFirstDescendant(cf => cf.ByName(tier));
            Assert.NotNull(tierElement);
        }

        // And an "All models" section listing the additional models
        var allModels = picker!.FindFirstDescendant(cf => cf.ByAutomationId("AllModelsExpander"));
        Assert.NotNull(allModels);
        allModels!.Patterns.ExpandCollapse.Pattern.Expand();

        var allModelsList = picker.FindFirstDescendant(cf => cf.ByAutomationId("AllModelsList"));
        Assert.NotNull(allModelsList);
        Assert.NotEmpty(allModelsList!.FindAllChildren());
    }

    // Scenario: The assistant turn displays the model that produced it
    //   Given a completed assistant turn produced by "claude-sonnet-5"
    //   Then the turn shows the model label for "claude-sonnet-5"
    [Fact]
    public void The_assistant_turn_displays_the_model_that_produced_it()
    {
        var window = _fixture.MainWindow;
        var conversations = OpenConversationsView(window);

        // Select the "Balanced" tier (claude-sonnet-5) then complete a turn.
        var picker = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ModelPicker"));
        Assert.NotNull(picker);
        var balanced = picker!.FindFirstDescendant(cf => cf.ByName("Balanced"))?.AsButton();
        Assert.NotNull(balanced);
        balanced!.Click();

        var input = conversations.FindFirstDescendant(cf => cf.ByAutomationId("MessageInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = "Hello";
        var sendButton = conversations.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"))?.AsButton();
        Assert.NotNull(sendButton);
        sendButton!.Click();

        // Then the turn shows the model label for "claude-sonnet-5"
        var thread = conversations.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"));
        Assert.NotNull(thread);
        var modelLabel = thread!.FindFirstDescendant(cf => cf.ByName("claude-sonnet-5"));
        Assert.NotNull(modelLabel);
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

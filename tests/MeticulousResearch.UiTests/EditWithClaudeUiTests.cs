using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/edit-with-claude/tests.md (SPEC §3.4, §9.1(5)). Drives the real
/// WPF window via FlaUI (UIA3) and requires a desktop session, so it is tagged <c>Category=ui</c> and
/// excluded from the headless gate; it must compile and build. It opens a project workspace's
/// Artifacts section, opens the artifact editor, and asserts the "Edit with Claude" instruction bar
/// and its per-edit model selector are present.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class EditWithClaudeUiTests
{
    private readonly ShellUiFixture _fixture;

    public EditWithClaudeUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The artifact editor exposes an "Edit with Claude" prompt bar
    //   Given the artifact editor is open
    //   Then an "Edit with Claude" instruction bar is available with a model selector
    [Fact]
    public void The_artifact_editor_exposes_an_edit_with_claude_prompt_bar()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);
        OpenFirstArtifact(artifacts);

        // An "Edit with Claude" instruction bar is available.
        var bar = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("EditWithClaudeBar"));
        Assert.NotNull(bar);

        var instruction = bar!.FindFirstDescendant(cf => cf.ByAutomationId("EditWithClaudeInstruction"));
        Assert.NotNull(instruction);

        // With a model selector.
        var modelPicker = bar.FindFirstDescendant(cf => cf.ByAutomationId("EditWithClaudeModelPicker"));
        Assert.NotNull(modelPicker);

        var editButton = bar.FindFirstDescendant(cf => cf.ByAutomationId("EditWithClaudeButton"))?.AsButton();
        Assert.NotNull(editButton);
    }

    /// <summary>Opens the artifact editor for the first artifact in the list.</summary>
    private static void OpenFirstArtifact(AutomationElement artifacts)
    {
        var list = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("ArtifactsList"));
        Assert.NotNull(list);
        var firstArtifact = list!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        firstArtifact?.Click();
    }

    /// <summary>
    /// Opens a project workspace and switches to the Artifacts section, returning the center pane.
    /// Fails loudly if the project-open seam (projects-crud) is missing so the test is never silently
    /// green.
    /// </summary>
    private static AutomationElement OpenArtifactsView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Artifacts"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

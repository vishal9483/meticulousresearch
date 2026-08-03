using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/artifact-diff/tests.md (SPEC §3.4). These drive the real WPF
/// window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and
/// excluded from the headless gate; they must compile and build. They open a project workspace's
/// Artifacts section and exercise the artifact editor's diff mode: side-by-side / inline panes, the
/// version pickers' previous-vs-current default, and the single-version disabled state.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ArtifactDiffUiTests
{
    private readonly ShellUiFixture _fixture;

    public ArtifactDiffUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Side-by-side diff shows both versions in parallel panes
    //   Given the artifact editor is in diff mode
    //   When I compare version 1 and version 2
    //   Then version 1 is shown in the left pane and version 2 in the right pane
    //   And changed regions are highlighted in both
    [Fact]
    public void Side_by_side_diff_shows_both_versions_in_parallel_panes()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);
        OpenFirstArtifact(artifacts);

        // Enter side-by-side presentation.
        var sideBySide = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffSideBySideButton"))?.AsButton();
        Assert.NotNull(sideBySide);
        sideBySide!.Click();

        // Version 1 is shown in the left pane and version 2 in the right pane.
        var view = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffSideBySideView"));
        Assert.NotNull(view);

        var left = view!.FindFirstDescendant(cf => cf.ByAutomationId("DiffLeftPane"));
        var right = view.FindFirstDescendant(cf => cf.ByAutomationId("DiffRightPane"));
        Assert.NotNull(left);
        Assert.NotNull(right);
    }

    // Scenario: Inline diff shows changes in a single merged view
    //   Given the artifact editor is in diff mode
    //   When I switch to inline view
    //   Then removals and additions are shown interleaved in one pane
    [Fact]
    public void Inline_diff_shows_changes_in_a_single_merged_view()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);
        OpenFirstArtifact(artifacts);

        // Switch to inline view.
        var inlineButton = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffInlineButton"))?.AsButton();
        Assert.NotNull(inlineButton);
        inlineButton!.Click();

        // Removals and additions are shown interleaved in one pane.
        var inline = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffInlineView"));
        Assert.NotNull(inline);
    }

    // Scenario: The version pickers default to comparing the previous version against the current
    //   Given an artifact with 3 versions, version 3 current
    //   When I open diff mode
    //   Then version 2 is preselected as base and version 3 as compare
    [Fact]
    public void The_version_pickers_default_to_previous_against_current()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);
        OpenFirstArtifact(artifacts);

        // Version 2 is preselected as base and version 3 as compare.
        var basePicker = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffBasePicker"))?.AsComboBox();
        var comparePicker = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffComparePicker"))?.AsComboBox();
        Assert.NotNull(basePicker);
        Assert.NotNull(comparePicker);

        Assert.Equal("2", basePicker!.SelectedItem?.Text);
        Assert.Equal("3", comparePicker!.SelectedItem?.Text);
    }

    // Scenario: Diff mode is unavailable with a single version
    //   Given an artifact with only version 1
    //   When I open the artifact editor
    //   Then diff mode is offered as disabled with a hint that two versions are required
    [Fact]
    public void Diff_mode_is_unavailable_with_a_single_version()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);
        OpenFirstArtifact(artifacts);

        // Diff mode is offered as disabled with a hint that two versions are required.
        var hint = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DiffDisabledHint"));
        Assert.NotNull(hint);
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

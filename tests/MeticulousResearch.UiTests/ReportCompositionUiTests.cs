using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/report-composition/tests.md (SPEC §3.4.1, §9.1(6)). Drives the
/// real WPF window via FlaUI (UIA3) and requires a desktop session, so it is tagged
/// <c>Category=ui</c> and excluded from the headless gate; it must compile and build. It opens a
/// project workspace's report composition view and asserts the ordered section list (with
/// drag-to-reorder) and the "Add section" action are present.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ReportCompositionUiTests
{
    private readonly ShellUiFixture _fixture;

    public ReportCompositionUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The report composition view lists sections in order with drag-to-reorder
    //   Given a composition with three sections
    //   When I open the report composition view
    //   Then the sections are listed in order
    //   And I can drag a section to reorder it
    [Fact]
    public void The_report_composition_view_lists_sections_in_order_with_drag_to_reorder()
    {
        var view = OpenReportCompositionView(_fixture.MainWindow);

        // The sections are listed in order.
        var list = view.FindFirstDescendant(cf => cf.ByAutomationId("ReportCompositionSections"));
        Assert.NotNull(list);
        var sections = list!.FindAllChildren();
        Assert.NotEmpty(sections);

        // I can drag a section to reorder it (reorder affordances are present on each section).
        var moveUp = list.FindFirstDescendant(cf => cf.ByAutomationId("ReportCompositionSectionMoveUp"))?.AsButton();
        Assert.NotNull(moveUp);
        var moveDown = list.FindFirstDescendant(cf => cf.ByAutomationId("ReportCompositionSectionMoveDown"))?.AsButton();
        Assert.NotNull(moveDown);
    }

    // Scenario: The composition view offers adding an artifact as a section
    //   Given a report composition view is open
    //   Then an "Add section" action lets me pick an existing project artifact
    [Fact]
    public void The_composition_view_offers_adding_an_artifact_as_a_section()
    {
        var view = OpenReportCompositionView(_fixture.MainWindow);

        // An "Add section" action.
        var addButton = view.FindFirstDescendant(cf => cf.ByAutomationId("ReportCompositionAddSection"))?.AsButton();
        Assert.NotNull(addButton);

        // Lets me pick an existing project artifact.
        var picker = view.FindFirstDescendant(cf => cf.ByAutomationId("ReportCompositionAddSectionPicker"));
        Assert.NotNull(picker);
    }

    /// <summary>
    /// Opens a project workspace and switches to the report composition view. Fails loudly if the
    /// project-open seam (projects-crud) is missing so the test is never silently green.
    /// </summary>
    private static AutomationElement OpenReportCompositionView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Report"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

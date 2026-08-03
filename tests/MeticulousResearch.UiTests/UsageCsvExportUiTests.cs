using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/usage-csv-export/tests.md (SPEC §3.6, §9.1(7) — export a
/// project's usage as a per-turn CSV). Drives the real WPF window via FlaUI (UIA3); tagged
/// <c>Category=ui</c> so it is excluded from the headless gate but must compile and build. It opens
/// the project dashboard, invokes the consolidated cost panel's "Export usage CSV" action, and
/// asserts a confirmation is shown.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class UsageCsvExportUiTests
{
    private readonly ShellUiFixture _fixture;

    public UsageCsvExportUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Usage CSV is exportable from the project dashboard cost panel
    //   Given the project dashboard for "EV Market 2026" is open
    //   When I choose "Export usage CSV" and pick a destination
    //   Then a CSV file is written to that destination
    //   And a confirmation is shown
    [Fact]
    public void Usage_CSV_is_exportable_from_the_project_dashboard_cost_panel()
    {
        // Given the project dashboard is open with its consolidated cost panel.
        var dashboard = OpenDashboardView(_fixture.MainWindow);
        var panel = dashboard.FindFirstDescendant(cf => cf.ByAutomationId("ConsolidatedCostPanel"))
            ?? throw new NotSupportedException(
                "The dashboard cost panel is owned by cost-tracking; wire this test to it when available.");

        // When I choose "Export usage CSV" (the destination picker is a shell-level save dialog).
        var exportButton = panel.FindFirstDescendant(cf => cf.ByAutomationId("ExportUsageCsvButton"))?.AsButton();
        Assert.NotNull(exportButton);
        exportButton!.Click();

        // Then a confirmation is shown that a CSV file was written to the chosen destination.
        var confirmation = panel.FindFirstDescendant(cf => cf.ByAutomationId("ExportUsageCsvConfirmation"));
        Assert.NotNull(confirmation);
        Assert.NotNull(confirmation!.AsLabel().Text);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Dashboard section, returning the center pane
    /// content. Fails loudly if the projects-crud open seam is missing.
    /// </summary>
    private static AutomationElement OpenDashboardView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Dashboard"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        return workspace;
    }
}

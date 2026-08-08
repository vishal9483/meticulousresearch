using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace MeticulousResearch.UiTests;

/// <summary>
/// Shared FlaUI navigation flows for @ui tests. Projects-crud, app-shell-navigation and the
/// project sections are implemented, so these drive the real create/open-project and section
/// affordances instead of the old loud seams. The @ui collection shares one app instance, so every
/// flow first returns to a known state (the Projects home) before acting.
/// </summary>
internal static class ShellUiFlow
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Navigates to the Projects home and returns its root element.</summary>
    public static AutomationElement EnsureAtHome(Window window)
    {
        var home = window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot"));
        if (home is null)
        {
            window.FindFirstDescendant(cf => cf.ByName("Projects"))?.AsButton()?.Invoke();
            home = Retry.WhileNull(
                () => window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot")),
                Timeout).Result!;
        }

        // Clear any leftover project-search filter so the full list is visible for the next flow.
        var search = home.FindFirstDescendant(cf => cf.ByAutomationId("ProjectSearch"))?.AsTextBox();
        if (search is not null && !string.IsNullOrEmpty(search.Text))
            search.Text = string.Empty;

        return home;
    }

    /// <summary>
    /// Opens the seeded, populated sample project (resources + an artifact) and returns its
    /// <c>WorkspaceRoot</c>. Deterministic regardless of prior test state: it returns to the home,
    /// filters the list to the sample project by name, and opens it.
    /// </summary>
    public static AutomationElement OpenSampleProject(Window window)
    {
        EnsureAtHome(window);

        var search = window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectSearch"))?.AsTextBox();
        if (search is not null)
            search.Text = MeticulousResearch.Core.Onboarding.SampleContent.ProjectName;

        var open = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByName("Open"))?.AsButton(),
            Timeout).Result;
        open!.Invoke();

        return Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot")),
            Timeout).Result!;
    }

    /// <summary>
    /// Creates and opens a fresh, empty project (a unique name so it never collides with the seeded
    /// sample), returning its <c>WorkspaceRoot</c>. Used by empty-state scenarios.
    /// </summary>
    public static AutomationElement OpenEmptyProject(Window window)
    {
        EnsureAtHome(window);

        var nameBox = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectName"))?.AsTextBox(),
            Timeout).Result;
        nameBox!.Text = "Empty " + Guid.NewGuid().ToString("N").Substring(0, 8);
        window.FindFirstDescendant(cf => cf.ByAutomationId("CreateProjectButton"))?.AsButton()?.Invoke();

        return Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot")),
            Timeout).Result!;
    }

    /// <summary>
    /// Opens a project workspace and selects the named left-nav section, returning the
    /// <c>CenterPane</c> content element.
    /// </summary>
    public static AutomationElement OpenSection(Window window, string section)
    {
        var workspace = OpenSampleProject(window);

        // Scope the section lookup to the workspace's left nav so an equally-named shell control
        // (e.g. the app-level "Settings" entry) is never matched instead of the section radio.
        var navItem = Retry.WhileNull(
            () => workspace.FindFirstDescendant(cf => cf.ByName(section))?.AsRadioButton(),
            Timeout).Result;
        navItem!.Click();

        return Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane")),
            Timeout).Result!;
    }
}

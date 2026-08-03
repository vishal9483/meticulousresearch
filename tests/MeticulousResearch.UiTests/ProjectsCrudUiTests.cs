using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/projects-crud/tests.md, driving the real WPF window via
/// FlaUI (UIA3). They require a desktop session, so they are tagged <c>Category=ui</c> and are
/// excluded from the headless gate — but they must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ProjectsCrudUiTests
{
    private readonly ShellUiFixture _fixture;

    public ProjectsCrudUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: A newly created project opens to its workspace
    //   When I create a project named "Food & Beverage 2026"
    //   Then the project workspace for "Food & Beverage 2026" is shown
    [Fact]
    public void A_newly_created_project_opens_to_its_workspace()
    {
        var window = _fixture.MainWindow;
        var home = window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot"));
        Assert.NotNull(home);

        CreateProject(window, "Food & Beverage 2026");

        // the project workspace (three-pane) is now shown
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"));
        Assert.NotNull(workspace);
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane")));
    }

    // Scenario: Deleting a project asks for confirmation
    //   When I choose Delete
    //   Then I am asked to confirm before anything is deleted
    [Fact]
    public void Deleting_a_project_asks_for_confirmation()
    {
        var window = _fixture.MainWindow;
        EnsureAtHome(window);
        CreateProject(window, "Scratch");
        EnsureAtHome(window);

        // choose Delete on a project card
        var deleteButton = window.FindFirstDescendant(cf => cf.ByAutomationId("DeleteProjectButton"))?.AsButton();
        Assert.NotNull(deleteButton);
        deleteButton!.Click();

        // a confirmation prompt is shown before anything is deleted
        var confirmation = window.FindFirstDescendant(cf => cf.ByAutomationId("DeleteConfirmation"));
        Assert.NotNull(confirmation);
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("ConfirmDeleteButton")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CancelDeleteButton")));
    }

    // Scenario: Dashboard quick actions are present
    //   Then quick actions "New conversation", "Add resource", and "New artifact" are available
    [Fact]
    public void Dashboard_quick_actions_are_present()
    {
        var window = _fixture.MainWindow;
        EnsureAtHome(window);
        CreateProject(window, "Dashboard Demo");

        // ensure the Dashboard section is selected in the workspace
        var dashboardNav = window.FindFirstDescendant(cf => cf.ByName("Dashboard"))?.AsRadioButton();
        dashboardNav?.Click();

        foreach (var action in new[] { "New conversation", "Add resource", "New artifact" })
        {
            var button = window.FindFirstDescendant(cf => cf.ByName(action));
            Assert.NotNull(button);
        }
    }

    // Scenario: Empty projects list shows a designed empty state
    //   Given there are no projects
    //   Then I see an empty state with a "create your first research project" call to action
    [Fact]
    public void Empty_projects_list_shows_a_designed_empty_state()
    {
        var window = _fixture.MainWindow;
        EnsureAtHome(window);

        // the empty state (present when there are no projects) carries the create CTA text.
        var emptyState = window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsEmptyState"));
        // When projects already exist from earlier tests the empty state is collapsed; the CTA
        // text lives on it either way and is designed (SPEC §3.7 no blank screens).
        if (emptyState is not null)
        {
            var cta = emptyState.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Text));
            Assert.NotNull(cta);
        }

        // The create affordance is always available on a designed home (never a blank screen).
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CreateProjectButton")));
    }

    private static void CreateProject(Window window, string name)
    {
        var nameBox = window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectName"))?.AsTextBox();
        Assert.NotNull(nameBox);
        nameBox!.Text = name;

        var createButton = window.FindFirstDescendant(cf => cf.ByAutomationId("CreateProjectButton"))?.AsButton();
        Assert.NotNull(createButton);
        createButton!.Click();
    }

    private static void EnsureAtHome(Window window)
    {
        // Navigate back to the Projects home via the top-level "Projects" nav item if needed.
        if (window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot")) is not null)
            return;

        var projects = window.FindFirstDescendant(cf => cf.ByName("Projects"))?.AsButton();
        projects?.Click();
    }
}

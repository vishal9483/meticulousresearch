using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Tests.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the shell + navigation contract (docs/features/app-shell-navigation/tests.md).
/// All are driven through view-models with no WPF window (TESTING-STRATEGY §2).
/// </summary>
public class ShellViewModelTests
{
    private static ShellViewModel NewShell() => new(TestNavigationServiceFactory.Create());

    // Scenario: Shell exposes the primary navigation regions
    //   Given the main shell view-model is initialized
    //   Then it exposes a top-level navigation with "Projects" as the root
    //   And a content region bound to the current view-model
    [Fact]
    public void Shell_exposes_the_primary_navigation_regions()
    {
        var shell = NewShell();

        // top-level navigation with "Projects" as the root
        Assert.NotEmpty(shell.NavigationItems);
        Assert.Equal("Projects", shell.RootNavItem.Label);
        Assert.Equal("Projects", shell.NavigationItems[0].Label);

        // a content region bound to the current view-model (populated on startup — Projects home)
        Assert.IsType<ProjectsHomeViewModel>(shell.CurrentViewModel);
    }

    // Scenario: Navigating sets the current view-model
    //   Given a shell view-model with a registered navigation service
    //   When I navigate to the "Resources" section of a project
    //   Then the shell's CurrentViewModel is a ResourcesViewModel scoped to that project
    [Fact]
    public void Navigating_sets_the_current_view_model()
    {
        var shell = NewShell();

        shell.NavigateToSection("P1", NavigationSection.Resources);

        var resources = Assert.IsType<ResourcesViewModel>(shell.CurrentViewModel);
        Assert.Equal("P1", resources.ProjectId);
    }

    // Scenario: Navigating to a project the shell records it as active
    //   Given a shell view-model
    //   When I navigate into project with id "P1"
    //   Then the shell's ActiveProjectId is "P1"
    [Fact]
    public void Navigating_into_a_project_records_it_as_active()
    {
        var shell = NewShell();

        shell.OpenProject("P1");

        Assert.Equal("P1", shell.ActiveProjectId);
    }

    // Scenario: Back navigation returns to the previous view
    //   Given I navigated Projects home -> project "P1" -> Resources
    //   When I invoke back navigation
    //   Then the current view is the "P1" workspace default view
    [Fact]
    public void Back_navigation_returns_to_the_previous_view()
    {
        var shell = NewShell();               // starts on Projects home
        var workspace = shell.OpenProject("P1");   // -> project "P1" workspace (default section)
        shell.NavigateToSection("P1", NavigationSection.Resources); // -> Resources

        shell.Back();

        // current view is the "P1" workspace default view
        var current = Assert.IsType<ProjectWorkspaceViewModel>(shell.CurrentViewModel);
        Assert.Same(workspace, current);
        Assert.Equal("P1", current.ProjectId);
        Assert.Equal(ProjectWorkspaceViewModel.DefaultSection, current.ActiveSection);
    }
}

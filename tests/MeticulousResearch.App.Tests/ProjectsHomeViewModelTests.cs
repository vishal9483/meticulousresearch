using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Tests.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the Projects home view-model (docs/features/projects-crud/tests.md). These
/// back the create/validation and "opens to its workspace" scenarios without a WPF window, using
/// an in-memory fake <see cref="IProjectService"/> and the real navigation service.
/// </summary>
public class ProjectsHomeViewModelTests
{
    private static (ProjectsHomeViewModel vm, FakeProjectService svc, NavigationService nav) NewHome()
    {
        var svc = new FakeProjectService();
        var nav = TestNavigationServiceFactory.Create();
        return (new ProjectsHomeViewModel(svc, nav), svc, nav);
    }

    // Scenario: Project name is required
    //   When I try to create a project with an empty name
    //   Then I see an inline validation error
    //   And no project is created
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Project_name_is_required(string emptyName)
    {
        var (vm, svc, _) = NewHome();
        vm.NewProjectName = emptyName;

        vm.CreateProjectCommand.Execute(null);

        Assert.True(vm.HasValidationError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ValidationError));
        Assert.Equal(0, svc.CreateCount);
    }

    // Scenario: Creating a blank project with a name (Projects home is open)
    [Fact]
    public void Creating_a_project_with_a_name_adds_it_and_clears_the_form()
    {
        var (vm, svc, _) = NewHome();
        vm.NewProjectName = "Automotive EV 2026";

        vm.CreateProjectCommand.Execute(null);

        Assert.Equal(1, svc.CreateCount);
        Assert.Contains(svc.Projects, p => p.Name == "Automotive EV 2026");
        Assert.Equal("", vm.NewProjectName);
        Assert.False(vm.HasValidationError);
    }

    // Supports @ui "A newly created project opens to its workspace":
    // after creation the navigation lands on the workspace scoped to the new project.
    [Fact]
    public void Creating_a_project_opens_its_workspace()
    {
        var (vm, svc, nav) = NewHome();
        vm.NewProjectName = "Food & Beverage 2026";

        vm.CreateProjectCommand.Execute(null);

        var workspace = Assert.IsType<ProjectWorkspaceViewModel>(nav.CurrentViewModel);
        var created = svc.Projects.Single();
        Assert.Equal(created.Id, workspace.ProjectId);
        Assert.Equal(created.Id, nav.ActiveProjectId);
    }

    // Supports @ui "Empty projects list shows a designed empty state":
    // with no projects the home reports empty and exposes a create call-to-action.
    [Fact]
    public void Empty_project_list_reports_empty_state_with_call_to_action()
    {
        var (vm, _, _) = NewHome();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasProjects);
        Assert.Contains("research project", vm.EmptyStateCallToAction, StringComparison.OrdinalIgnoreCase);
    }

    // The "Show archived" toggle includes/excludes archived projects in the list.
    [Fact]
    public void Show_archived_toggle_controls_which_projects_are_listed()
    {
        var (vm, svc, _) = NewHome();
        var active = svc.Create("Active");
        var archived = svc.Create("Archived");
        svc.Archive(archived.Id);
        vm.Refresh();

        Assert.Contains(vm.Projects, p => p.Id == active.Id);
        Assert.DoesNotContain(vm.Projects, p => p.Id == archived.Id);

        vm.ShowArchived = true;

        Assert.Contains(vm.Projects, p => p.Id == archived.Id);
    }

    // Supports @ui "Deleting a project asks for confirmation":
    // requesting a delete arms confirmation but removes nothing until confirmed.
    [Fact]
    public void Requesting_delete_asks_for_confirmation_before_removing()
    {
        var (vm, svc, _) = NewHome();
        var project = svc.Create("Scratch");
        vm.Refresh();

        vm.RequestDeleteProjectCommand.Execute(project.Id);

        Assert.True(vm.IsConfirmingDelete);
        Assert.Equal(0, svc.DeleteCount);          // nothing deleted yet
        Assert.Contains(svc.Projects, p => p.Id == project.Id);

        vm.ConfirmDeleteCommand.Execute(null);

        Assert.False(vm.IsConfirmingDelete);
        Assert.Equal(1, svc.DeleteCount);
        Assert.DoesNotContain(svc.Projects, p => p.Id == project.Id);
    }

    [Fact]
    public void Cancelling_delete_removes_nothing()
    {
        var (vm, svc, _) = NewHome();
        var project = svc.Create("Scratch");
        vm.Refresh();

        vm.RequestDeleteProjectCommand.Execute(project.Id);
        vm.CancelDeleteCommand.Execute(null);

        Assert.False(vm.IsConfirmingDelete);
        Assert.Equal(0, svc.DeleteCount);
        Assert.Contains(svc.Projects, p => p.Id == project.Id);
    }
}

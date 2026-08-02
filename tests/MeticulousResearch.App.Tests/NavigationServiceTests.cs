using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Tests.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the <see cref="NavigationService"/> contract that downstream features consume.
/// </summary>
public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_sets_current_view_model_and_returns_instance()
    {
        var nav = TestNavigationServiceFactory.Create();

        var vm = nav.NavigateTo<ProjectsHomeViewModel>();

        Assert.Same(vm, nav.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_project_scoped_vm_sets_active_project_id()
    {
        var nav = TestNavigationServiceFactory.Create();

        nav.NavigateTo<ResourcesViewModel>("P1");

        Assert.Equal("P1", nav.ActiveProjectId);
    }

    [Fact]
    public void NavigateTo_non_project_scoped_vm_clears_active_project_id()
    {
        var nav = TestNavigationServiceFactory.Create();
        nav.NavigateTo<ResourcesViewModel>("P1");

        nav.NavigateTo<ProjectsHomeViewModel>();

        Assert.Null(nav.ActiveProjectId);
    }

    [Fact]
    public void CanGoBack_is_false_with_one_entry_and_true_with_more()
    {
        var nav = TestNavigationServiceFactory.Create();
        nav.NavigateTo<ProjectsHomeViewModel>();
        Assert.False(nav.CanGoBack);

        nav.NavigateTo<ResourcesViewModel>("P1");
        Assert.True(nav.CanGoBack);
    }

    [Fact]
    public void Back_reactivates_the_previous_entry()
    {
        var nav = TestNavigationServiceFactory.Create();
        var home = nav.NavigateTo<ProjectsHomeViewModel>();
        nav.NavigateTo<ResourcesViewModel>("P1");

        nav.Back();

        Assert.Same(home, nav.CurrentViewModel);
        Assert.Null(nav.ActiveProjectId); // home is not project-scoped
    }

    [Fact]
    public void Back_is_a_no_op_at_the_root()
    {
        var nav = TestNavigationServiceFactory.Create();
        var home = nav.NavigateTo<ProjectsHomeViewModel>();

        nav.Back();

        Assert.Same(home, nav.CurrentViewModel);
    }

    [Fact]
    public void CurrentViewModel_raises_property_changed()
    {
        var nav = TestNavigationServiceFactory.Create();
        var raised = new List<string?>();
        nav.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        nav.NavigateTo<ProjectsHomeViewModel>();

        Assert.Contains(nameof(INavigationService.CurrentViewModel), raised);
    }
}

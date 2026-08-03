using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the three-pane workspace view-model. These back the @ui workspace/left-nav
/// scenarios by proving the underlying section-switching logic without a window.
/// </summary>
public class ProjectWorkspaceViewModelTests
{
    // Supports @ui "Opening a project shows the three-pane workspace":
    // the left pane lists exactly these sections, in order.
    [Fact]
    public void Workspace_lists_the_five_sections_in_order()
    {
        var ws = new ProjectWorkspaceViewModel("P1");

        Assert.Equal(
            new[] { "Conversations", "Resources", "Artifacts", "Dashboard", "Settings" },
            ws.Sections.Select(s => s.Title).ToArray());
    }

    // Supports @ui "center pane shows the project's default view".
    [Fact]
    public void Workspace_opens_on_the_default_section()
    {
        var ws = new ProjectWorkspaceViewModel("P1");

        Assert.Equal(NavigationSection.Dashboard, ProjectWorkspaceViewModel.DefaultSection);
        Assert.Equal(ProjectWorkspaceViewModel.DefaultSection, ws.ActiveSection);
        Assert.IsType<DashboardViewModel>(ws.CurrentSection);
    }

    // Supports @ui "Left-nav switches the center pane" (Scenario Outline).
    [Theory]
    [InlineData(NavigationSection.Conversations, typeof(ConversationsViewModel))]
    [InlineData(NavigationSection.Resources, typeof(ResourcesViewModel))]
    [InlineData(NavigationSection.Artifacts, typeof(ArtifactsViewModel))]
    [InlineData(NavigationSection.Dashboard, typeof(DashboardViewModel))]
    public void Selecting_a_section_swaps_the_center_pane_and_marks_it_active(
        NavigationSection section, Type expectedVmType)
    {
        var ws = new ProjectWorkspaceViewModel("P1");

        ws.SelectSection(section);

        Assert.IsType(expectedVmType, ws.CurrentSection);
        Assert.Equal(section, ws.ActiveSection); // "selected nav item is visually marked active"
    }

    [Fact]
    public void All_sections_are_scoped_to_the_project()
    {
        var ws = new ProjectWorkspaceViewModel("P1");

        Assert.All(ws.Sections, s => Assert.Equal("P1", s.ProjectId));
    }
}

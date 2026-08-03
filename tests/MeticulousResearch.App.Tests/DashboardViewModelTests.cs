using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the project dashboard view-model (docs/features/projects-crud/tests.md).
/// Backs the "@unit dashboard reports counts and last activity" behaviour at the view-model
/// layer and the "@ui quick actions are present" scenario (the three quick-action labels).
/// </summary>
public class DashboardViewModelTests
{
    // Scenario: Project dashboard reports counts and last activity (view-model surface)
    [Fact]
    public void Dashboard_surfaces_counts_and_last_activity_from_the_service()
    {
        var last = new DateTimeOffset(2026, 8, 3, 12, 5, 0, TimeSpan.Zero);
        var svc = new FakeProjectService();
        svc.SetDashboard(new ProjectDashboard("P1", ResourceCount: 3, ConversationCount: 2, ArtifactCount: 1, last));

        var vm = new DashboardViewModel("P1", svc);

        Assert.Equal(3, vm.ResourceCount);
        Assert.Equal(2, vm.ConversationCount);
        Assert.Equal(1, vm.ArtifactCount);
        Assert.Equal(last, vm.LastActivity);
    }

    // Scenario: Dashboard quick actions are present
    //   Then quick actions "New conversation", "Add resource", and "New artifact" are available
    [Fact]
    public void Dashboard_exposes_the_three_quick_actions()
    {
        var vm = new DashboardViewModel("P1");

        Assert.Equal(
            new[] { "New conversation", "Add resource", "New artifact" },
            vm.QuickActions.ToArray());
    }
}

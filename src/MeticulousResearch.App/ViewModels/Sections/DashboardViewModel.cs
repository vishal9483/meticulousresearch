using System.Collections.ObjectModel;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Project dashboard — the default view when a project opens: counts, last activity, and
/// quick actions (SPEC §3.1, §3.6). The consolidated cost panel is added later by
/// <c>cost-tracking</c>; a slot is left for it. Minimal but designed.
/// </summary>
public sealed class DashboardViewModel : SectionViewModel
{
    /// <summary>The quick-action labels shown on the dashboard (SPEC §3.1).</summary>
    public static readonly string[] QuickActionLabels =
        { "New conversation", "Add resource", "New artifact" };

    /// <summary>
    /// Creates the Dashboard section for <paramref name="projectId"/> without live figures
    /// (window-free plumbing / design-time). Delegates to the service-aware constructor.
    /// </summary>
    public DashboardViewModel(string projectId) : this(projectId, null)
    {
    }

    /// <summary>
    /// Creates the Dashboard section for <paramref name="projectId"/>. When a
    /// <paramref name="projects"/> service is supplied the counts and last-activity figures are
    /// loaded; otherwise they default to zero.
    /// </summary>
    public DashboardViewModel(string projectId, IProjectService? projects) : base(projectId)
    {
        QuickActions = new ReadOnlyCollection<string>(QuickActionLabels);
        if (projects is not null)
            LoadFrom(projects);
    }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Dashboard;

    /// <inheritdoc />
    public override string Title => "Dashboard";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Project overview: resources, artifacts, activity and cost.";

    /// <summary>The dashboard quick actions (New conversation / Add resource / New artifact).</summary>
    public ReadOnlyCollection<string> QuickActions { get; }

    /// <summary>Number of resources attached to the project.</summary>
    public int ResourceCount { get; private set; }

    /// <summary>Number of conversations in the project.</summary>
    public int ConversationCount { get; private set; }

    /// <summary>Number of artifacts in the project.</summary>
    public int ArtifactCount { get; private set; }

    /// <summary>The most recent activity instant across the project, or <c>null</c> when none.</summary>
    public DateTimeOffset? LastActivity { get; private set; }

    /// <summary>Loads the dashboard figures for this project from the service.</summary>
    public void LoadFrom(IProjectService projects)
    {
        var dashboard = projects.GetDashboard(ProjectId);
        ResourceCount = dashboard.ResourceCount;
        ConversationCount = dashboard.ConversationCount;
        ArtifactCount = dashboard.ArtifactCount;
        LastActivity = dashboard.LastActivity;
        OnPropertyChanged(nameof(ResourceCount));
        OnPropertyChanged(nameof(ConversationCount));
        OnPropertyChanged(nameof(ArtifactCount));
        OnPropertyChanged(nameof(LastActivity));
    }
}

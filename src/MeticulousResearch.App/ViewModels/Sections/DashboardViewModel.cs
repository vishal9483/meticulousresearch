using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Project dashboard — the default view when a project opens: counts, last activity, and
/// (later) consolidated cost (SPEC §3.1, §3.6). Minimal but designed.
/// </summary>
public sealed class DashboardViewModel : SectionViewModel
{
    /// <summary>Creates the Dashboard section for <paramref name="projectId"/>.</summary>
    public DashboardViewModel(string projectId) : base(projectId) { }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Dashboard;

    /// <inheritdoc />
    public override string Title => "Dashboard";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Project overview: resources, artifacts, activity and cost.";
}

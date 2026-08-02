namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The Projects home — the landing screen (SPEC §4.1): the grid/list of research projects.
/// Not project-scoped (it is where the user picks a project). Minimal but designed; real
/// project cards land with the projects-crud feature.
/// </summary>
public sealed class ProjectsHomeViewModel : ViewModelBase
{
    /// <summary>Title shown in the content region header.</summary>
    public string Title => "Projects";

    /// <summary>Designed empty/landing headline (SPEC §3.7 no blank screens).</summary>
    public string Headline => "Your research projects — open one or create a new project.";
}

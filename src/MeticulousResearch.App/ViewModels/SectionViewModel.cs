using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// Base for the project-scoped section view-models shown in the center pane of the three-pane
/// workspace (Conversations / Resources / Artifacts / Dashboard / Settings). Carries the
/// project id and the section identity; later features flesh out each section's real content
/// without changing this shape.
/// </summary>
public abstract class SectionViewModel : ViewModelBase, IProjectScoped, INavigationAware
{
    /// <summary>Creates a section view-model scoped to <paramref name="projectId"/>.</summary>
    protected SectionViewModel(string projectId)
    {
        ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
    }

    /// <inheritdoc />
    public string ProjectId { get; private set; }

    /// <summary>Which left-nav section this view-model backs.</summary>
    public abstract NavigationSection Section { get; }

    /// <summary>Human-readable section title, shown in the left nav and as the pane header.</summary>
    public abstract string Title { get; }

    /// <summary>
    /// Accepts a project id passed as the first navigation parameter, so sections resolved via
    /// <see cref="INavigationService.NavigateTo{TViewModel}"/> land scoped to the right project.
    /// </summary>
    public virtual void OnNavigatedTo(object[] parameters)
    {
        if (parameters is { Length: > 0 } && parameters[0] is string projectId && projectId.Length > 0)
        {
            ProjectId = projectId;
        }
    }
}

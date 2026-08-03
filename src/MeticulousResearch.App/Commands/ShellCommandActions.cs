using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App.Commands;

/// <summary>
/// The shell wiring for the core palette commands (SPEC §3.5). Routes each core command to an
/// existing destination through the shared <see cref="INavigationService"/>: the create commands
/// open the relevant screen/section where that action lives, and search opens the search surface.
/// The palette invokes; it does not reimplement the create/search flows.
/// </summary>
public sealed class ShellCommandActions : ICommandActions
{
    private readonly INavigationService _navigation;

    /// <summary>Creates the shell action wiring over the navigation service.</summary>
    public ShellCommandActions(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    /// <inheritdoc />
    public void NewProject() => _navigation.NavigateTo<ProjectsHomeViewModel>();

    /// <inheritdoc />
    public void NewConversation() => NavigateToActiveSection(NavigationSection.Conversations);

    /// <inheritdoc />
    public void NewArtifact() => NavigateToActiveSection(NavigationSection.Artifacts);

    /// <inheritdoc />
    public void OpenSearch() => NavigateToActiveSection(NavigationSection.Resources);

    private void NavigateToActiveSection(NavigationSection section)
    {
        var projectId = _navigation.ActiveProjectId;
        if (projectId is null)
        {
            // No project open: land on the Projects home so the action is never a dead end.
            _navigation.NavigateTo<ProjectsHomeViewModel>();
            return;
        }

        switch (section)
        {
            case NavigationSection.Conversations:
                _navigation.NavigateTo<ConversationsViewModel>(projectId);
                break;
            case NavigationSection.Artifacts:
                _navigation.NavigateTo<ArtifactsViewModel>(projectId);
                break;
            case NavigationSection.Resources:
                _navigation.NavigateTo<ResourcesViewModel>(projectId);
                break;
        }
    }
}

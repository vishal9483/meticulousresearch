using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The main shell view-model (SPEC §4, §7.1). Owns the top-level navigation rail (root:
/// "Projects") and exposes the content region — <see cref="CurrentViewModel"/> — which is bound
/// to the injected <see cref="INavigationService"/>. Window-free so it is <c>@unit</c>-testable.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>
    /// Creates the shell over a navigation service and lands on the Projects home
    /// (SPEC §4.1 — Projects home is the startup view).
    /// </summary>
    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        NavigationItems = new ReadOnlyObservableCollection<TopLevelNavItem>(
            new ObservableCollection<TopLevelNavItem>
            {
                new("Projects", "Projects"),
            });
        RootNavItem = NavigationItems[0];

        // Re-raise so views binding to the shell update when the current VM / back state changes.
        _navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(INavigationService.CurrentViewModel))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
            }
            else if (e.PropertyName is nameof(INavigationService.ActiveProjectId))
            {
                OnPropertyChanged(nameof(ActiveProjectId));
            }
            else if (e.PropertyName is nameof(INavigationService.CanGoBack))
            {
                OnPropertyChanged(nameof(CanGoBack));
                BackCommand.NotifyCanExecuteChanged();
            }
        };

        GoHome();
    }

    /// <summary>The top-level navigation items; the first is the root, "Projects".</summary>
    public ReadOnlyObservableCollection<TopLevelNavItem> NavigationItems { get; }

    /// <summary>The root top-level nav item ("Projects").</summary>
    public TopLevelNavItem RootNavItem { get; }

    /// <summary>The content region — the view-model currently shown (bound to the nav service).</summary>
    public ViewModelBase? CurrentViewModel => _navigation.CurrentViewModel;

    /// <summary>The id of the project the user is currently inside, or <c>null</c> at the home.</summary>
    public string? ActiveProjectId => _navigation.ActiveProjectId;

    /// <summary>True when back navigation is possible.</summary>
    public bool CanGoBack => _navigation.CanGoBack;

    /// <summary>Navigates to the Projects home (SPEC §4.1).</summary>
    [RelayCommand]
    public void GoHome() => _navigation.NavigateTo<ProjectsHomeViewModel>();

    /// <summary>Navigates the content region to the app-level Settings screen (SPEC §3.5, §4(7)).</summary>
    [RelayCommand]
    public void GoToSettings() => _navigation.NavigateTo<SettingsViewModel>();

    /// <summary>
    /// Opens a project's three-pane workspace, landing on its default section and recording the
    /// project as active. Passing the project id as the first navigation parameter scopes the
    /// workspace and (via <see cref="IProjectScoped"/>) sets <see cref="ActiveProjectId"/>.
    /// </summary>
    /// <param name="projectId">The id of the project to open.</param>
    /// <returns>The activated workspace view-model.</returns>
    public ProjectWorkspaceViewModel OpenProject(string projectId)
        => _navigation.NavigateTo<ProjectWorkspaceViewModel>(projectId);

    /// <summary>
    /// Navigates the shell's content region directly to a section of a project (e.g. Resources),
    /// so <see cref="CurrentViewModel"/> becomes that section's view-model scoped to the project.
    /// </summary>
    /// <param name="projectId">The project the section belongs to.</param>
    /// <param name="section">The section to show.</param>
    /// <returns>The activated section view-model.</returns>
    public SectionViewModel NavigateToSection(string projectId, NavigationSection section) => section switch
    {
        NavigationSection.Conversations => _navigation.NavigateTo<Sections.ConversationsViewModel>(projectId),
        NavigationSection.Resources => _navigation.NavigateTo<Sections.ResourcesViewModel>(projectId),
        NavigationSection.Artifacts => _navigation.NavigateTo<Sections.ArtifactsViewModel>(projectId),
        NavigationSection.Dashboard => _navigation.NavigateTo<Sections.DashboardViewModel>(projectId),
        NavigationSection.Settings => _navigation.NavigateTo<Sections.ProjectSettingsViewModel>(projectId),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown navigation section."),
    };

    /// <summary>Returns to the previous destination on the back-stack.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void Back() => _navigation.Back();
}

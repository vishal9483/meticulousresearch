using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The Projects home — the landing screen (SPEC §4.1, §3.1): the grid/list of research projects
/// with create, search, and a "show archived" toggle, plus a designed empty state. Creating a
/// project opens straight into its workspace (SPEC §9.1(2)). Window-free so it is
/// <c>@unit</c>-testable; a parameterless constructor keeps navigation plumbing/design-time happy.
/// </summary>
public sealed partial class ProjectsHomeViewModel : ViewModelBase
{
    private readonly IProjectService? _projects;
    private readonly INavigationService? _navigation;

    /// <summary>Design-time / navigation-plumbing fallback (no service; empty list).</summary>
    public ProjectsHomeViewModel()
    {
    }

    /// <summary>Creates the Projects home over the project service and navigation.</summary>
    public ProjectsHomeViewModel(IProjectService projects, INavigationService navigation)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Refresh();
    }

    /// <summary>Title shown in the content region header.</summary>
    public string Title => "Projects";

    /// <summary>Designed landing headline (SPEC §3.7 no blank screens).</summary>
    public string Headline => "Your research projects — open one or create a new project.";

    /// <summary>Call-to-action shown by the designed empty state.</summary>
    public string EmptyStateCallToAction => "Create your first research project.";

    /// <summary>The project cards currently shown (after search + archived filtering).</summary>
    public ObservableCollection<ProjectListItemViewModel> Projects { get; } = new();

    /// <summary>True when no projects match the current filter — drives the empty state.</summary>
    public bool IsEmpty => Projects.Count == 0;

    /// <summary>True when at least one project matches the current filter.</summary>
    public bool HasProjects => Projects.Count > 0;

    private string _newProjectName = "";

    /// <summary>The name typed into the new-project form.</summary>
    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetProperty(ref _newProjectName, value))
            {
                // Typing clears a prior validation error.
                ValidationError = null;
            }
        }
    }

    private string? _validationError;

    /// <summary>The inline validation error for the new-project form, or <c>null</c> when valid.</summary>
    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetProperty(ref _validationError, value))
                OnPropertyChanged(nameof(HasValidationError));
        }
    }

    /// <summary>True when an inline validation error is showing.</summary>
    public bool HasValidationError => _validationError is not null;

    private bool _showArchived;

    /// <summary>Whether archived projects are included in the list ("Show archived" toggle).</summary>
    public bool ShowArchived
    {
        get => _showArchived;
        set
        {
            if (SetProperty(ref _showArchived, value))
                Refresh();
        }
    }

    private string _searchQuery = "";

    /// <summary>The project search filter (name/description).</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                Refresh();
        }
    }

    /// <summary>
    /// Creates a project from the new-project form. A blank name shows an inline validation error
    /// and creates nothing; a valid name creates the project and opens its workspace.
    /// </summary>
    [RelayCommand]
    public void CreateProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            ValidationError = "Project name is required.";
            return;
        }

        if (_projects is null)
            return;

        var project = _projects.Create(NewProjectName.Trim());
        NewProjectName = "";
        Refresh();
        OpenProject(project.Id);
    }

    /// <summary>Opens a project's three-pane workspace.</summary>
    [RelayCommand]
    public void OpenProject(string projectId)
    {
        _navigation?.NavigateTo<ProjectWorkspaceViewModel>(projectId);
    }

    /// <summary>Archives a project and refreshes the list.</summary>
    [RelayCommand]
    public void ArchiveProject(string projectId)
    {
        _projects?.Archive(projectId);
        Refresh();
    }

    private string? _pendingDeleteId;

    /// <summary>The name of the project pending delete confirmation, or <c>null</c> when none.</summary>
    public string? PendingDeleteName { get; private set; }

    /// <summary>True while a delete is awaiting confirmation (drives the confirmation prompt).</summary>
    public bool IsConfirmingDelete => _pendingDeleteId is not null;

    /// <summary>
    /// Begins deleting a project: records it as pending and asks the user to confirm. Nothing is
    /// removed until <see cref="ConfirmDelete"/> is invoked (SPEC §3.1 delete requires confirm).
    /// </summary>
    [RelayCommand]
    public void RequestDeleteProject(string projectId)
    {
        _pendingDeleteId = projectId;
        PendingDeleteName = Projects.FirstOrDefault(p => p.Id == projectId)?.Name
            ?? _projects?.Get(projectId)?.Name;
        OnPropertyChanged(nameof(IsConfirmingDelete));
        OnPropertyChanged(nameof(PendingDeleteName));
    }

    /// <summary>Confirms and performs the pending delete, then refreshes the list.</summary>
    [RelayCommand]
    public void ConfirmDelete()
    {
        if (_pendingDeleteId is null)
            return;

        _projects?.Delete(_pendingDeleteId);
        ClearPendingDelete();
        Refresh();
    }

    /// <summary>Cancels a pending delete without removing anything.</summary>
    [RelayCommand]
    public void CancelDelete() => ClearPendingDelete();

    private void ClearPendingDelete()
    {
        _pendingDeleteId = null;
        PendingDeleteName = null;
        OnPropertyChanged(nameof(IsConfirmingDelete));
        OnPropertyChanged(nameof(PendingDeleteName));
    }

    /// <summary>Reloads the project list applying the current search + archived filter.</summary>
    public void Refresh()
    {
        Projects.Clear();
        if (_projects is null)
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasProjects));
            return;
        }

        var source = string.IsNullOrWhiteSpace(SearchQuery)
            ? _projects.List(includeArchived: ShowArchived)
            : _projects.Search(SearchQuery).Where(p => ShowArchived || !p.Archived);

        foreach (var p in source)
            Projects.Add(new ProjectListItemViewModel(p.Id, p.Name, p.Description, p.Color, p.Archived));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasProjects));
    }
}

/// <summary>A single project card in the Projects home list.</summary>
public sealed class ProjectListItemViewModel : ObservableObject
{
    /// <summary>Creates a project card.</summary>
    public ProjectListItemViewModel(string id, string name, string? description, string? color, bool archived)
    {
        Id = id;
        Name = name;
        Description = description;
        Color = color;
        Archived = archived;
    }

    /// <summary>The project id.</summary>
    public string Id { get; }

    /// <summary>The project name.</summary>
    public string Name { get; }

    /// <summary>The optional project description.</summary>
    public string? Description { get; }

    /// <summary>The optional accent color token.</summary>
    public string? Color { get; }

    /// <summary>Whether the project is archived.</summary>
    public bool Archived { get; }
}

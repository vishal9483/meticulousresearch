using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Backup;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Export;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Search;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The three-pane project workspace (SPEC §4.2): a left nav of sections, a center pane bound to
/// the active section's view-model, and a right contextual pane. Owns the child section
/// view-models and swaps the center <see cref="CurrentSection"/> when the user selects a section.
/// Window-free so it is <c>@unit</c>-testable.
/// </summary>
public sealed partial class ProjectWorkspaceViewModel : ViewModelBase, IProjectScoped, INavigationAware
{
    /// <summary>The section shown by default when a project opens (SPEC §4.2 default view).</summary>
    public const NavigationSection DefaultSection = NavigationSection.Dashboard;

    private readonly Dictionary<NavigationSection, SectionViewModel> _sections;
    private readonly IProjectBackupService? _backup;

    /// <summary>
    /// Builds a workspace for <paramref name="projectId"/> without live dashboard figures
    /// (window-free plumbing / design-time). Delegates to the service-aware constructor.
    /// </summary>
    public ProjectWorkspaceViewModel(string projectId) : this(projectId, null)
    {
    }

    /// <summary>
    /// Builds a workspace for <paramref name="projectId"/>, wiring up the five section
    /// view-models. All are project-scoped; the Dashboard is selected by default. When a
    /// <paramref name="projects"/> service is supplied the Dashboard is populated with live
    /// counts; otherwise sections render their designed defaults. A supplied
    /// <paramref name="conversations"/> service backs the Conversations section's send flow.
    /// </summary>
    public ProjectWorkspaceViewModel(
        string projectId,
        IProjectService? projects,
        IConversationService? conversations = null,
        ISettingsService? settings = null,
        IModelCatalog? catalog = null,
        IStreamingConversationService? streaming = null,
        ICostService? cost = null,
        IResourceService? resources = null,
        ISearchService? search = null,
        IArtifactService? artifacts = null,
        IArtifactDiffService? diffService = null,
        IEditWithClaudeService? editService = null,
        IExportService? exportService = null,
        ITurnActionService? turnActions = null,
        ITurnCostCalculator? turnCostCalculator = null,
        IClipboardService? clipboard = null,
        RetryStatusViewModel? retryStatus = null,
        IContextBudgetService? budgetService = null,
        IProjectBackupService? backup = null)
    {
        ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _backup = backup;

        _sections = new Dictionary<NavigationSection, SectionViewModel>
        {
            [NavigationSection.Conversations] = new ConversationsViewModel(projectId, conversations, settings, catalog, streaming, turnActions, turnCostCalculator, clipboard, retryStatus, cost, resources, budgetService),
            [NavigationSection.Resources] = new ResourcesViewModel(projectId, resources, search),
            [NavigationSection.Artifacts] = new ArtifactsViewModel(projectId, artifacts, diffService, editService, catalog, exportService),
            [NavigationSection.Dashboard] = new DashboardViewModel(projectId, projects, cost),
            [NavigationSection.Settings] = new ProjectSettingsViewModel(projectId),
        };

        // Left-nav order per SPEC §4.2: Conversations, Resources, Artifacts, Dashboard, Settings.
        Sections = new ReadOnlyObservableCollection<SectionViewModel>(new ObservableCollection<SectionViewModel>
        {
            _sections[NavigationSection.Conversations],
            _sections[NavigationSection.Resources],
            _sections[NavigationSection.Artifacts],
            _sections[NavigationSection.Dashboard],
            _sections[NavigationSection.Settings],
        });

        SelectSection(DefaultSection);
    }

    /// <inheritdoc />
    public string ProjectId { get; private set; }

    /// <summary>The left-nav section view-models, in display order.</summary>
    public ReadOnlyObservableCollection<SectionViewModel> Sections { get; }

    private SectionViewModel _currentSection = null!;

    /// <summary>The view-model shown in the center pane (the active section).</summary>
    public SectionViewModel CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(ActiveSection));
            }
        }
    }

    /// <summary>Which section is currently active (drives the "active" visual state in the nav).</summary>
    public NavigationSection ActiveSection => _currentSection.Section;

    /// <summary>Swaps the center pane to the given section. Bound from the left-nav.</summary>
    /// <param name="section">The section to activate.</param>
    [RelayCommand]
    public void SelectSection(NavigationSection section)
    {
        CurrentSection = _sections[section];
    }

    /// <summary>Returns the section view-model for the given section (used by @ui/no-placeholder tests).</summary>
    public SectionViewModel GetSection(NavigationSection section) => _sections[section];

    private string _backupConfirmation = "";

    /// <summary>A confirmation shown after a successful backup (backup-restore, SPEC §8, §9.1(9)).</summary>
    public string BackupConfirmation
    {
        get => _backupConfirmation;
        private set => SetProperty(ref _backupConfirmation, value);
    }

    /// <summary>
    /// Backs up this project to <paramref name="destinationZip"/> (backup-restore, SPEC §8,
    /// §9.1(9)) and raises a confirmation. The destination picker is a shell-level dialog owned by
    /// the view; this method is window-free so it stays <c>@unit</c>-testable.
    /// </summary>
    /// <param name="destinationZip">The absolute path of the backup zip to write.</param>
    public void BackupProject(string destinationZip)
    {
        if (_backup is null)
            return;
        _backup.Backup(ProjectId, destinationZip);
        BackupConfirmation = $"Backup written to {destinationZip}";
    }

    /// <summary>
    /// Accepts the project id (first navigation parameter) and, optionally, an initial
    /// <see cref="NavigationSection"/> to open (second parameter).
    /// </summary>
    public void OnNavigatedTo(object[] parameters)
    {
        if (parameters is { Length: > 1 } && parameters[1] is NavigationSection section)
        {
            SelectSection(section);
        }
    }
}

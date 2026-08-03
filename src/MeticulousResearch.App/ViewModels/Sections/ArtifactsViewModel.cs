using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Artifacts section — substantial, versioned, standalone deliverables the analyst curates
/// (SPEC §3.4). This slice lists the project's artifacts, exposes a "New artifact" entry point, and
/// renders a designed empty state when the project has no artifacts. Window-free so the flow is
/// <c>@unit</c>-testable; the artifact domain itself is owned by <see cref="IArtifactService"/>.
/// </summary>
public sealed partial class ArtifactsViewModel : SectionViewModel
{
    private readonly IArtifactService? _artifacts;
    private readonly IArtifactDiffService? _diffService;
    private readonly IEditWithClaudeService? _editService;
    private readonly IModelCatalog? _catalog;

    /// <summary>Designed empty-state message shown when the project has no artifacts yet.</summary>
    public const string EmptyStateMessage =
        "No artifacts yet. Generate one from a prompt, promote a conversation turn, or start a blank draft.";

    /// <summary>Design-time / window-free constructor without a service (renders the empty state).</summary>
    public ArtifactsViewModel(string projectId) : this(projectId, null) { }

    /// <summary>Creates the Artifacts section wired to the artifact domain service.</summary>
    public ArtifactsViewModel(string projectId, IArtifactService? artifacts)
        : this(projectId, artifacts, null) { }

    /// <summary>Creates the Artifacts section wired to the artifact domain and diff services.</summary>
    public ArtifactsViewModel(string projectId, IArtifactService? artifacts, IArtifactDiffService? diffService)
        : this(projectId, artifacts, diffService, null, null) { }

    /// <summary>
    /// Creates the Artifacts section wired to the artifact domain, diff, and edit-with-Claude
    /// services plus the model catalog backing the per-edit model selector (SPEC §3.4).
    /// </summary>
    public ArtifactsViewModel(
        string projectId,
        IArtifactService? artifacts,
        IArtifactDiffService? diffService,
        IEditWithClaudeService? editService,
        IModelCatalog? catalog)
        : base(projectId)
    {
        _artifacts = artifacts;
        _diffService = diffService;
        _editService = editService;
        _catalog = catalog;
        Artifacts = new ObservableCollection<ArtifactRowViewModel>();
        Artifacts.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasArtifacts));
        };
        Load();
    }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Artifacts;

    /// <inheritdoc />
    public override string Title => "Artifacts";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Versioned, exportable research deliverables.";

    /// <summary>The artifacts in this project, most recently created first.</summary>
    public ObservableCollection<ArtifactRowViewModel> Artifacts { get; }

    /// <summary>Whether the project currently has no artifacts (drives the designed empty state).</summary>
    public bool IsEmpty => Artifacts.Count == 0;

    /// <summary>Whether the project has at least one artifact (drives the list's visibility).</summary>
    public bool HasArtifacts => Artifacts.Count > 0;

    private ArtifactRowViewModel? _selectedArtifact;

    /// <summary>The artifact selected for editing (the "editor" destination), or null.</summary>
    public ArtifactRowViewModel? SelectedArtifact
    {
        get => _selectedArtifact;
        set
        {
            if (SetProperty(ref _selectedArtifact, value))
            {
                BuildDiff();
                BuildEditBar();
            }
        }
    }

    private EditWithClaudeViewModel? _editWithClaude;

    /// <summary>
    /// The "Edit with Claude" prompt bar for the selected artifact (SPEC §3.4, §9.1(5)), or null when
    /// no artifact is selected or the edit service/catalog is unavailable.
    /// </summary>
    public EditWithClaudeViewModel? EditWithClaude
    {
        get => _editWithClaude;
        private set => SetProperty(ref _editWithClaude, value);
    }

    private void BuildEditBar()
    {
        if (_editService is null || _catalog is null || _selectedArtifact is null)
        {
            EditWithClaude = null;
            return;
        }

        EditWithClaude = new EditWithClaudeViewModel(_selectedArtifact.Id, _editService, _catalog);
    }

    private ArtifactDiffViewModel? _diff;

    /// <summary>
    /// The diff-mode view-model for the selected artifact (SPEC §3.4), or null when no artifact is
    /// selected or the diff service is unavailable. Defaults to previous-vs-current and is disabled
    /// when the artifact has a single version.
    /// </summary>
    public ArtifactDiffViewModel? Diff
    {
        get => _diff;
        private set => SetProperty(ref _diff, value);
    }

    private void BuildDiff()
    {
        if (_artifacts is null || _diffService is null || _selectedArtifact is null)
        {
            Diff = null;
            return;
        }

        Diff = new ArtifactDiffViewModel(_artifacts.GetHistory(_selectedArtifact.Id), _diffService);
    }

    /// <inheritdoc />
    public override void OnNavigatedTo(object[] parameters)
    {
        base.OnNavigatedTo(parameters);
        Load();
    }

    /// <summary>
    /// Starts a new blank <c>doc</c> artifact and selects it, so the "New artifact" call to action
    /// always lands on a real editor destination (SPEC §3.4 creation path 4).
    /// </summary>
    [RelayCommand]
    private void NewArtifact()
    {
        if (_artifacts is null)
            return;

        var created = _artifacts.Create(ProjectId, ArtifactTypes.Doc, "Untitled artifact");
        Load();
        SelectedArtifact = Artifacts.FirstOrDefault(a => a.Id == created.Id);
    }

    private void Load()
    {
        Artifacts.Clear();
        if (_artifacts is null)
            return;

        foreach (var artifact in _artifacts.List(ProjectId))
            Artifacts.Add(new ArtifactRowViewModel(artifact));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasArtifacts));
    }
}

/// <summary>A single artifact row in the Artifacts list (title + type).</summary>
public sealed class ArtifactRowViewModel
{
    /// <summary>Projects a persisted <see cref="Artifact"/> into a list row.</summary>
    public ArtifactRowViewModel(Artifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Id = artifact.Id;
        Title = artifact.Title;
        Type = artifact.Type;
    }

    /// <summary>The artifact id.</summary>
    public string Id { get; }

    /// <summary>The artifact title.</summary>
    public string Title { get; }

    /// <summary>The artifact type (doc/text/code/table/diagram).</summary>
    public string Type { get; }
}

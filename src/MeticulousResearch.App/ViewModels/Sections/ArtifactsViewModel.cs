using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Export;
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
    private readonly IExportService? _exportService;

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
        : this(projectId, artifacts, diffService, editService, catalog, null) { }

    /// <summary>
    /// Creates the Artifacts section wired to the artifact domain, diff, edit-with-Claude, model
    /// catalog, and branded export services (SPEC §3.4, §3.4.2).
    /// </summary>
    public ArtifactsViewModel(
        string projectId,
        IArtifactService? artifacts,
        IArtifactDiffService? diffService,
        IEditWithClaudeService? editService,
        IModelCatalog? catalog,
        IExportService? exportService)
        : base(projectId)
    {
        _artifacts = artifacts;
        _diffService = diffService;
        _editService = editService;
        _catalog = catalog;
        _exportService = exportService;
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
                IsConfirmingDelete = false;
                BuildDiff();
                BuildEditBar();
                BuildExportBar();
                BuildVersionRail();
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>Whether an artifact is selected (drives the editor's version rail / manage actions).</summary>
    public bool HasSelection => _selectedArtifact is not null;

    /// <summary>The selected artifact's version history, newest-first, for the version rail (SPEC §3.4).</summary>
    public ObservableCollection<ArtifactVersionRowViewModel> Versions { get; } =
        new ObservableCollection<ArtifactVersionRowViewModel>();

    private void BuildVersionRail()
    {
        Versions.Clear();
        if (_artifacts is null || _selectedArtifact is null)
            return;

        var artifact = _artifacts.Get(_selectedArtifact.Id);
        if (artifact is null)
            return;

        foreach (var version in _artifacts.GetHistory(_selectedArtifact.Id))
            Versions.Add(new ArtifactVersionRowViewModel(version, isCurrent: version.Id == artifact.CurrentVersionId));
    }

    private bool _isConfirmingDelete;

    /// <summary>Whether the delete-confirmation prompt is shown (nothing is deleted until confirmed).</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set => SetProperty(ref _isConfirmingDelete, value);
    }

    /// <summary>Opens the delete-confirmation prompt for the selected artifact; deletes nothing yet.</summary>
    [RelayCommand]
    private void RequestDeleteArtifact()
    {
        if (_selectedArtifact is null)
            return;
        IsConfirmingDelete = true;
    }

    /// <summary>Confirms deletion: removes the selected artifact and its history, then reloads.</summary>
    [RelayCommand]
    private void ConfirmDeleteArtifact()
    {
        if (_artifacts is null || _selectedArtifact is null)
            return;

        _artifacts.DeleteArtifact(_selectedArtifact.Id);
        IsConfirmingDelete = false;
        Load();
    }

    /// <summary>Dismisses the delete-confirmation prompt without deleting anything.</summary>
    [RelayCommand]
    private void CancelDeleteArtifact() => IsConfirmingDelete = false;

    private EditWithClaudeViewModel? _editWithClaude;

    /// <summary>
    /// The "Edit with Claude" prompt bar for the selected artifact (SPEC §3.4, §9.1(5)), or null when
    /// no artifact is selected or the edit service/catalog is unavailable.
    /// </summary>
    public EditWithClaudeViewModel? EditWithClaude
    {
        get => _editWithClaude;
        private set
        {
            if (SetProperty(ref _editWithClaude, value))
                OnPropertyChanged(nameof(HasEditWithClaude));
        }
    }

    /// <summary>Whether the edit-with-Claude bar is available for the selected artifact.</summary>
    public bool HasEditWithClaude => _editWithClaude is not null;

    private void BuildEditBar()
    {
        if (_editService is null || _catalog is null || _selectedArtifact is null)
        {
            EditWithClaude = null;
            return;
        }

        EditWithClaude = new EditWithClaudeViewModel(_selectedArtifact.Id, _editService, _catalog);
    }

    private BrandedExportViewModel? _export;

    /// <summary>
    /// The branded export menu for the selected artifact (SPEC §3.4.2, §9.1(6)), or null when no
    /// artifact is selected or the artifact/export service is unavailable.
    /// </summary>
    public BrandedExportViewModel? Export
    {
        get => _export;
        private set
        {
            if (SetProperty(ref _export, value))
                OnPropertyChanged(nameof(HasExport));
        }
    }

    /// <summary>Whether the branded-export bar is available for the selected artifact.</summary>
    public bool HasExport => _export is not null;

    private void BuildExportBar()
    {
        if (_artifacts is null || _exportService is null || _selectedArtifact is null)
        {
            Export = null;
            return;
        }

        var artifact = _artifacts.Get(_selectedArtifact.Id);
        if (artifact is null)
        {
            Export = null;
            return;
        }

        var current = _artifacts.GetHistory(artifact.Id)
            .FirstOrDefault(v => v.Id == artifact.CurrentVersionId);
        var content = current?.Content ?? "";
        var source = ExportSource.FromArtifact(
            new ExportArtifact(artifact.Title, artifact.Type, content),
            project: ProjectId);
        Export = new BrandedExportViewModel(source, _exportService, BrandSettings.Unset);
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
        private set
        {
            if (SetProperty(ref _diff, value))
                OnPropertyChanged(nameof(HasDiff));
        }
    }

    /// <summary>Whether diff mode is available for the selected artifact (the panel is shown).</summary>
    public bool HasDiff => _diff is not null;

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
        {
            SelectedArtifact = null;
            return;
        }

        foreach (var artifact in _artifacts.List(ProjectId))
            Artifacts.Add(new ArtifactRowViewModel(artifact));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasArtifacts));

        // Land on a real editor destination so the version rail, diff, edit, and export affordances
        // are populated as soon as the section opens (SPEC §3.4 editor).
        SelectedArtifact = Artifacts.FirstOrDefault();
    }
}

/// <summary>A single version row in the artifact editor's version-history rail (SPEC §3.4).</summary>
public sealed class ArtifactVersionRowViewModel
{
    /// <summary>Projects a persisted <see cref="ArtifactVersion"/> into a rail row.</summary>
    public ArtifactVersionRowViewModel(ArtifactVersion version, bool isCurrent)
    {
        ArgumentNullException.ThrowIfNull(version);
        Id = version.Id;
        VersionNo = version.VersionNo;
        CreatedBy = version.CreatedBy;
        IsCurrent = isCurrent;
    }

    /// <summary>The version id.</summary>
    public string Id { get; }

    /// <summary>The 1-based per-artifact version number.</summary>
    public long VersionNo { get; }

    /// <summary>Who produced the version (<c>user</c> | <c>claude</c>).</summary>
    public string CreatedBy { get; }

    /// <summary>Whether this is the artifact's current version (drives the current-version marker).</summary>
    public bool IsCurrent { get; }

    /// <summary>Display label for the rail row.</summary>
    public string Label => $"Version {VersionNo} · {CreatedBy}";
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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;

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

    /// <summary>Designed empty-state message shown when the project has no artifacts yet.</summary>
    public const string EmptyStateMessage =
        "No artifacts yet. Generate one from a prompt, promote a conversation turn, or start a blank draft.";

    /// <summary>Design-time / window-free constructor without a service (renders the empty state).</summary>
    public ArtifactsViewModel(string projectId) : this(projectId, null) { }

    /// <summary>Creates the Artifacts section wired to the artifact domain service.</summary>
    public ArtifactsViewModel(string projectId, IArtifactService? artifacts) : base(projectId)
    {
        _artifacts = artifacts;
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
        set => SetProperty(ref _selectedArtifact, value);
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

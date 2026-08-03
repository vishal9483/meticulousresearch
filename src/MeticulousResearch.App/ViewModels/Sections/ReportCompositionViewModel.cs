using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Reports;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The report composition view (SPEC §3.4.1, §4 screen 5c): an ordered list of section references
/// with reorder, add/remove, and pin-version controls, plus a designed empty state guiding the
/// analyst to add sections. Window-free so the flow is <c>@unit</c>-testable; the composition domain
/// itself is owned by <see cref="IReportCompositionService"/>.
/// </summary>
public sealed partial class ReportCompositionViewModel : ObservableObject
{
    /// <summary>Designed empty-state guidance shown when the composition has no sections yet.</summary>
    public const string EmptyStatePrompt =
        "This report has no sections yet. Add an existing project artifact as a section to begin composing.";

    private readonly IReportCompositionService _compositions;
    private readonly IArtifactService _artifacts;
    private readonly string _compositionId;
    private readonly string _projectId;

    /// <summary>Creates the composition view over the composition and artifact services.</summary>
    /// <param name="compositionId">The composition artifact this view edits.</param>
    /// <param name="projectId">The owning project (source of artifacts available to add as sections).</param>
    /// <param name="compositions">The report composition domain service.</param>
    /// <param name="artifacts">The artifact service used to list artifacts available to add.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public ReportCompositionViewModel(
        string compositionId, string projectId, IReportCompositionService compositions, IArtifactService artifacts)
    {
        _compositionId = compositionId ?? throw new ArgumentNullException(nameof(compositionId));
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));

        Sections = new ObservableCollection<ReportSection>();
        AvailableArtifacts = new ObservableCollection<Artifact>();
        Sections.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSections));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyStateMessage));
        };
        Reload();
    }

    /// <summary>The composition's sections in order.</summary>
    public ObservableCollection<ReportSection> Sections { get; }

    /// <summary>Project artifacts the analyst can add as sections.</summary>
    public ObservableCollection<Artifact> AvailableArtifacts { get; }

    /// <summary>Whether the composition currently has at least one section.</summary>
    public bool HasSections => Sections.Count > 0;

    /// <summary>Whether the composition is empty (shows the add-sections guidance).</summary>
    public bool IsEmpty => Sections.Count == 0;

    /// <summary>The empty-state guidance when empty, otherwise an empty string.</summary>
    public string EmptyStateMessage => IsEmpty ? EmptyStatePrompt : "";

    /// <summary>Adds the artifact <paramref name="artifactId"/> as a new last section.</summary>
    [RelayCommand]
    public void AddSection(string artifactId)
    {
        _compositions.AddSection(_compositionId, artifactId);
        Reload();
    }

    /// <summary>Removes section <paramref name="sectionId"/> from the composition.</summary>
    [RelayCommand]
    public void RemoveSection(string sectionId)
    {
        _compositions.RemoveSection(_compositionId, sectionId);
        Reload();
    }

    /// <summary>Moves section <paramref name="sectionId"/> one position earlier (drag-to-reorder up).</summary>
    [RelayCommand]
    public void MoveUp(string sectionId) => Move(sectionId, -1);

    /// <summary>Moves section <paramref name="sectionId"/> one position later (drag-to-reorder down).</summary>
    [RelayCommand]
    public void MoveDown(string sectionId) => Move(sectionId, +1);

    /// <summary>Pins section <paramref name="sectionId"/> to the fixed artifact version <paramref name="versionId"/>.</summary>
    public void PinVersion(string sectionId, string versionId)
    {
        _compositions.PinSectionVersion(_compositionId, sectionId, versionId);
        Reload();
    }

    private void Move(string sectionId, int delta)
    {
        var ids = Sections.Select(s => s.SectionId).ToList();
        var index = ids.IndexOf(sectionId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ids.Count)
            return;
        (ids[index], ids[target]) = (ids[target], ids[index]);
        _compositions.ReorderSections(_compositionId, ids);
        Reload();
    }

    private void Reload()
    {
        Sections.Clear();
        foreach (var section in _compositions.GetSections(_compositionId))
            Sections.Add(section);

        AvailableArtifacts.Clear();
        foreach (var artifact in _artifacts.List(_projectId))
        {
            if (artifact.Id == _compositionId)
                continue;
            AvailableArtifacts.Add(artifact);
        }
    }
}

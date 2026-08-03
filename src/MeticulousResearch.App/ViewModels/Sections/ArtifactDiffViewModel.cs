using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>How a computed diff is presented (SPEC §3.4).</summary>
public enum ArtifactDiffMode
{
    /// <summary>Two parallel panes: base on the left, compare on the right.</summary>
    SideBySide,

    /// <summary>A single merged pane with removals and additions interleaved.</summary>
    Inline,
}

/// <summary>
/// Diff mode of the artifact editor (SPEC §3.4): two version pickers over an artifact's history, a
/// side-by-side / inline presentation toggle, and the highlighted changed regions computed by
/// <see cref="IArtifactDiffService"/>. Read-only — it never creates a version or sets current.
/// Defaults to comparing the previous version against the current, and is disabled (with a hint)
/// when the artifact has only one version. Window-free so the flow is <c>@unit</c>-testable.
/// </summary>
public sealed partial class ArtifactDiffViewModel : ObservableObject
{
    private readonly IArtifactDiffService _diffService;

    /// <summary>Hint shown when diff mode is disabled because the artifact has a single version.</summary>
    public const string SingleVersionHint = "Diff needs at least two versions. Save another version to compare.";

    private ArtifactVersion? _baseVersion;
    private ArtifactVersion? _compareVersion;
    private ArtifactDiffMode _mode = ArtifactDiffMode.SideBySide;

    /// <summary>
    /// Creates diff mode over <paramref name="history"/> (an artifact's versions, newest-first as
    /// returned by <see cref="IArtifactService.GetHistory"/>), defaulting the pickers to the previous
    /// version (base) against the current version (compare).
    /// </summary>
    public ArtifactDiffViewModel(IReadOnlyList<ArtifactVersion> history, IArtifactDiffService diffService)
    {
        ArgumentNullException.ThrowIfNull(history);
        _diffService = diffService ?? throw new ArgumentNullException(nameof(diffService));

        // Present oldest → newest for the pickers so "previous vs current" reads naturally.
        Versions = new ObservableCollection<ArtifactVersion>(history.OrderBy(v => v.VersionNo));
        LeftSegments = new ObservableCollection<DiffSegment>();
        RightSegments = new ObservableCollection<DiffSegment>();
        InlineSegments = new ObservableCollection<DiffSegment>();

        if (IsAvailable)
        {
            // Current version is the newest; the default base is the one immediately before it.
            _compareVersion = Versions[^1];
            _baseVersion = Versions[^2];
            Recompute();
        }
    }

    /// <summary>The artifact's versions in ascending version order for the pickers.</summary>
    public ObservableCollection<ArtifactVersion> Versions { get; }

    /// <summary>Whether diff mode is available (needs at least two versions).</summary>
    public bool IsAvailable => Versions.Count >= 2;

    /// <summary>Whether diff mode is disabled because only one version exists.</summary>
    public bool IsDisabled => !IsAvailable;

    /// <summary>The disabled-state hint, or empty when diff mode is available.</summary>
    public string DisabledHint => IsAvailable ? "" : SingleVersionHint;

    /// <summary>The selected base version (the "old" side; removals are relative to it).</summary>
    public ArtifactVersion? BaseVersion
    {
        get => _baseVersion;
        set
        {
            if (SetProperty(ref _baseVersion, value))
                Recompute();
        }
    }

    /// <summary>The selected compare version (the "new" side; additions are relative to it).</summary>
    public ArtifactVersion? CompareVersion
    {
        get => _compareVersion;
        set
        {
            if (SetProperty(ref _compareVersion, value))
                Recompute();
        }
    }

    /// <summary>The current presentation mode (side-by-side or inline).</summary>
    public ArtifactDiffMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsSideBySide));
                OnPropertyChanged(nameof(IsInline));
            }
        }
    }

    /// <summary>Whether the side-by-side presentation is active.</summary>
    public bool IsSideBySide => Mode == ArtifactDiffMode.SideBySide;

    /// <summary>Whether the inline presentation is active.</summary>
    public bool IsInline => Mode == ArtifactDiffMode.Inline;

    /// <summary>The base version rendered in the left pane (unchanged + removed regions).</summary>
    public ObservableCollection<DiffSegment> LeftSegments { get; }

    /// <summary>The compare version rendered in the right pane (unchanged + added regions).</summary>
    public ObservableCollection<DiffSegment> RightSegments { get; }

    /// <summary>The merged inline view (unchanged, then removals and additions interleaved).</summary>
    public ObservableCollection<DiffSegment> InlineSegments { get; }

    /// <summary>Whether the current comparison found any differences.</summary>
    public bool HasChanges { get; private set; }

    /// <summary>Switches to the side-by-side presentation.</summary>
    [RelayCommand]
    private void ShowSideBySide() => Mode = ArtifactDiffMode.SideBySide;

    /// <summary>Switches to the inline (merged) presentation.</summary>
    [RelayCommand]
    private void ShowInline() => Mode = ArtifactDiffMode.Inline;

    private void Recompute()
    {
        LeftSegments.Clear();
        RightSegments.Clear();
        InlineSegments.Clear();
        HasChanges = false;

        if (_baseVersion is null || _compareVersion is null)
        {
            OnPropertyChanged(nameof(HasChanges));
            return;
        }

        var diff = _diffService.Diff(_baseVersion, _compareVersion);
        HasChanges = diff.HasChanges;

        foreach (var segment in diff.Segments)
        {
            InlineSegments.Add(segment);
            if (segment.Kind != DiffChangeKind.Added)
                LeftSegments.Add(segment);
            if (segment.Kind != DiffChangeKind.Removed)
                RightSegments.Add(segment);
        }

        OnPropertyChanged(nameof(HasChanges));
    }
}

using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// The read-only diff contract (SPEC §3.4): computes the differences between any two versions of an
/// artifact so an edit or regeneration can be reviewed before it is kept. Owned by
/// <c>artifact-diff</c>; it never creates versions or sets current — it reads the immutable history
/// produced by <c>artifact-versioning</c>. Direction matters: additions/removals are labeled from
/// the base version's perspective (base → compare).
/// </summary>
public interface IArtifactDiffService
{
    /// <summary>
    /// Diffs <paramref name="baseVersion"/> against <paramref name="compareVersion"/>, choosing a
    /// format-aware strategy from the base version's content format: a row/cell-aware diff for CSV
    /// tables, and a line-based text diff for doc/text/code/diagram (Mermaid) content.
    /// </summary>
    /// <param name="baseVersion">The "old" version; removals are relative to it.</param>
    /// <param name="compareVersion">The "new" version; additions are relative to it.</param>
    /// <returns>A deterministic, read-only diff of the two versions.</returns>
    ArtifactDiff Diff(ArtifactVersion baseVersion, ArtifactVersion compareVersion);
}

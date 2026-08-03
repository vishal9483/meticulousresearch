namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// The kind of change a diff region represents, from the base version's perspective
/// (base → compare direction) (SPEC §3.4).
/// </summary>
public enum DiffChangeKind
{
    /// <summary>The region is identical in both versions.</summary>
    Unchanged,

    /// <summary>The region exists only in the compare version (an addition).</summary>
    Added,

    /// <summary>The region exists only in the base version (a removal).</summary>
    Removed,
}

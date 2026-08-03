namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// One ordered region of a text diff: a piece of content tagged with how it changed between the
/// base and compare versions (SPEC §3.4). Consumed identically by the side-by-side and inline
/// renderers.
/// </summary>
/// <param name="Kind">Whether this region is unchanged, added, or removed.</param>
/// <param name="Text">The region's text (a whole line, or the added/removed fragment of a line).</param>
public sealed record DiffSegment(DiffChangeKind Kind, string Text);

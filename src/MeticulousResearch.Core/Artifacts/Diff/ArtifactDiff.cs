namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// The result of diffing two artifact versions (SPEC §3.4): an ordered list of text segments for
/// line-based formats (doc/text/code/diagram) or a structured <see cref="TableDiff"/> for CSV
/// tables. Pure and deterministic for given inputs; the same computation feeds both the
/// side-by-side and inline presentations.
/// </summary>
public sealed class ArtifactDiff
{
    private ArtifactDiff(string format, IReadOnlyList<DiffSegment> segments, TableDiff? table)
    {
        Format = format;
        Segments = segments;
        Table = table;
    }

    /// <summary>Creates a text (line-based) diff result.</summary>
    public static ArtifactDiff ForText(IReadOnlyList<DiffSegment> segments) =>
        new("text", segments, table: null);

    /// <summary>Creates a table (row/cell) diff result.</summary>
    public static ArtifactDiff ForTable(TableDiff table) =>
        new("table", Array.Empty<DiffSegment>(), table);

    /// <summary>The diff family: <c>text</c> (line-based) or <c>table</c> (row/cell).</summary>
    public string Format { get; }

    /// <summary>The ordered text segments (empty for table diffs).</summary>
    public IReadOnlyList<DiffSegment> Segments { get; }

    /// <summary>The structured table diff, or null for text diffs.</summary>
    public TableDiff? Table { get; }

    /// <summary>Whether the two versions differ at all.</summary>
    public bool HasChanges =>
        Table is not null ? Table.HasChanges : Segments.Any(s => s.Kind != DiffChangeKind.Unchanged);

    /// <summary>The added text segments (compare-only regions), in order.</summary>
    public IReadOnlyList<DiffSegment> AddedSegments =>
        Segments.Where(s => s.Kind == DiffChangeKind.Added).ToList();

    /// <summary>The removed text segments (base-only regions), in order.</summary>
    public IReadOnlyList<DiffSegment> RemovedSegments =>
        Segments.Where(s => s.Kind == DiffChangeKind.Removed).ToList();
}

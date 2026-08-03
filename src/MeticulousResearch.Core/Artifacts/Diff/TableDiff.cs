namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// A single cell whose value changed between the base and compare versions of a table artifact
/// (SPEC §3.4 row/cell-aware diff).
/// </summary>
/// <param name="Row">The 0-based row position of the changed cell (base-side).</param>
/// <param name="Column">The 0-based column position of the changed cell.</param>
/// <param name="BaseValue">The cell's value in the base version.</param>
/// <param name="CompareValue">The cell's value in the compare version.</param>
public sealed record TableCellChange(int Row, int Column, string BaseValue, string CompareValue);

/// <summary>
/// A whole row that was added or removed between the base and compare versions of a table artifact
/// (SPEC §3.4).
/// </summary>
/// <param name="Kind">Whether the row was added (compare-only) or removed (base-only).</param>
/// <param name="Cells">The row's cells.</param>
public sealed record TableRowChange(DiffChangeKind Kind, IReadOnlyList<string> Cells);

/// <summary>
/// The structured result of diffing two CSV table versions: rows added/removed and, for rows that
/// stayed but changed, the individual changed cells (SPEC §3.4).
/// </summary>
public sealed class TableDiff
{
    /// <summary>Creates a table diff from its row and cell change sets.</summary>
    public TableDiff(IReadOnlyList<TableRowChange> addedRows, IReadOnlyList<TableRowChange> removedRows, IReadOnlyList<TableCellChange> changedCells)
    {
        AddedRows = addedRows;
        RemovedRows = removedRows;
        ChangedCells = changedCells;
    }

    /// <summary>Rows present only in the compare version.</summary>
    public IReadOnlyList<TableRowChange> AddedRows { get; }

    /// <summary>Rows present only in the base version.</summary>
    public IReadOnlyList<TableRowChange> RemovedRows { get; }

    /// <summary>Cells that changed value between aligned rows.</summary>
    public IReadOnlyList<TableCellChange> ChangedCells { get; }

    /// <summary>Whether any row or cell differs between the two versions.</summary>
    public bool HasChanges => AddedRows.Count > 0 || RemovedRows.Count > 0 || ChangedCells.Count > 0;
}

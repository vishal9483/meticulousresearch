using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Artifacts.Diff;

/// <summary>
/// The pure, deterministic diff engine (SPEC §3.4). Line-based formats use an LCS line diff; a
/// replaced line that is a pure extension of its predecessor (the base line is a character
/// subsequence of the compare line) is reported as an addition only, so an additive edit records no
/// removal. CSV tables are diffed by rows (LCS over full-row equality) and, for aligned changed
/// rows, by individual cells. No dependencies, no I/O — safe to unit-test without a window.
/// </summary>
public sealed class ArtifactDiffService : IArtifactDiffService
{
    /// <inheritdoc />
    public ArtifactDiff Diff(ArtifactVersion baseVersion, ArtifactVersion compareVersion)
    {
        ArgumentNullException.ThrowIfNull(baseVersion);
        ArgumentNullException.ThrowIfNull(compareVersion);

        var format = baseVersion.ContentFormat;
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return ArtifactDiff.ForTable(DiffTable(baseVersion.Content, compareVersion.Content));

        return ArtifactDiff.ForText(DiffText(baseVersion.Content, compareVersion.Content));
    }

    /// <summary>Computes a line-based text diff of two contents (base → compare).</summary>
    internal static IReadOnlyList<DiffSegment> DiffText(string baseContent, string compareContent)
    {
        var baseLines = SplitLines(baseContent);
        var compareLines = SplitLines(compareContent);
        var ops = LcsDiff(baseLines, compareLines);

        var segments = new List<DiffSegment>();
        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i].Kind == OpKind.Equal)
            {
                segments.Add(new DiffSegment(DiffChangeKind.Unchanged, ops[i].Text));
                i++;
                continue;
            }

            var deletes = new List<string>();
            var inserts = new List<string>();
            while (i < ops.Count && ops[i].Kind != OpKind.Equal)
            {
                if (ops[i].Kind == OpKind.Delete)
                    deletes.Add(ops[i].Text);
                else
                    inserts.Add(ops[i].Text);
                i++;
            }

            EmitChangeBlock(segments, deletes, inserts);
        }

        return segments;
    }

    private static void EmitChangeBlock(List<DiffSegment> segments, List<string> deletes, List<string> inserts)
    {
        var paired = Math.Min(deletes.Count, inserts.Count);
        for (var k = 0; k < paired; k++)
        {
            var oldLine = deletes[k];
            var newLine = inserts[k];

            if (IsSubsequence(oldLine, newLine))
            {
                // Additive: the base line survives inside the compare line — only the appended
                // fragment is an addition; nothing is removed.
                var fragment = InsertedFragment(oldLine, newLine);
                segments.Add(new DiffSegment(DiffChangeKind.Added, fragment.Length > 0 ? fragment : newLine));
            }
            else if (IsSubsequence(newLine, oldLine))
            {
                // Reductive: the compare line survives inside the base line — only the deleted
                // fragment is a removal; nothing is added.
                var fragment = InsertedFragment(newLine, oldLine);
                segments.Add(new DiffSegment(DiffChangeKind.Removed, fragment.Length > 0 ? fragment : oldLine));
            }
            else
            {
                segments.Add(new DiffSegment(DiffChangeKind.Removed, oldLine));
                segments.Add(new DiffSegment(DiffChangeKind.Added, newLine));
            }
        }

        for (var k = paired; k < deletes.Count; k++)
            segments.Add(new DiffSegment(DiffChangeKind.Removed, deletes[k]));
        for (var k = paired; k < inserts.Count; k++)
            segments.Add(new DiffSegment(DiffChangeKind.Added, inserts[k]));
    }

    /// <summary>Computes a row/cell-aware CSV table diff of two contents (base → compare).</summary>
    internal static TableDiff DiffTable(string baseContent, string compareContent)
    {
        var baseRows = SplitLines(baseContent);
        var compareRows = SplitLines(compareContent);
        var ops = LcsDiff(baseRows, compareRows);

        var addedRows = new List<TableRowChange>();
        var removedRows = new List<TableRowChange>();
        var changedCells = new List<TableCellChange>();

        var i = 0;
        var baseRowIndex = 0;
        while (i < ops.Count)
        {
            if (ops[i].Kind == OpKind.Equal)
            {
                i++;
                baseRowIndex++;
                continue;
            }

            var deletes = new List<string>();
            var inserts = new List<string>();
            while (i < ops.Count && ops[i].Kind != OpKind.Equal)
            {
                if (ops[i].Kind == OpKind.Delete)
                    deletes.Add(ops[i].Text);
                else
                    inserts.Add(ops[i].Text);
                i++;
            }

            var paired = Math.Min(deletes.Count, inserts.Count);
            for (var k = 0; k < paired; k++)
            {
                var baseCells = SplitCells(deletes[k]);
                var compareCells = SplitCells(inserts[k]);
                var columns = Math.Max(baseCells.Length, compareCells.Length);
                for (var c = 0; c < columns; c++)
                {
                    var b = c < baseCells.Length ? baseCells[c] : "";
                    var d = c < compareCells.Length ? compareCells[c] : "";
                    if (!string.Equals(b, d, StringComparison.Ordinal))
                        changedCells.Add(new TableCellChange(baseRowIndex, c, b, d));
                }
                baseRowIndex++;
            }

            for (var k = paired; k < deletes.Count; k++)
            {
                removedRows.Add(new TableRowChange(DiffChangeKind.Removed, SplitCells(deletes[k])));
                baseRowIndex++;
            }
            for (var k = paired; k < inserts.Count; k++)
                addedRows.Add(new TableRowChange(DiffChangeKind.Added, SplitCells(inserts[k])));
        }

        return new TableDiff(addedRows, removedRows, changedCells);
    }

    private static string[] SplitCells(string row) =>
        row.Split(',').Select(c => c.Trim()).ToArray();

    private static string[] SplitLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<string>();
        return content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    /// <summary>Whether every character of <paramref name="small"/> appears, in order, within <paramref name="big"/>.</summary>
    private static bool IsSubsequence(string small, string big)
    {
        if (small.Length == 0)
            return true;
        var s = 0;
        for (var b = 0; b < big.Length && s < small.Length; b++)
        {
            if (big[b] == small[s])
                s++;
        }
        return s == small.Length;
    }

    /// <summary>
    /// Given that <paramref name="small"/> is a character subsequence of <paramref name="big"/>,
    /// returns the trimmed run(s) of <paramref name="big"/> that are not matched by
    /// <paramref name="small"/> — i.e. the inserted text — joined by spaces.
    /// </summary>
    private static string InsertedFragment(string small, string big)
    {
        var runs = new List<string>();
        var current = new System.Text.StringBuilder();
        var s = 0;
        foreach (var ch in big)
        {
            if (s < small.Length && ch == small[s])
            {
                if (current.Length > 0)
                {
                    runs.Add(current.ToString());
                    current.Clear();
                }
                s++;
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0)
            runs.Add(current.ToString());

        return string.Join(" ", runs.Select(r => r.Trim()).Where(r => r.Length > 0));
    }

    private enum OpKind
    {
        Equal,
        Delete,
        Insert,
    }

    private readonly record struct DiffOp(OpKind Kind, string Text);

    /// <summary>Classic LCS line diff producing an ordered op list (equal / delete / insert).</summary>
    private static List<DiffOp> LcsDiff(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var m = a.Count;
        var n = b.Count;
        var dp = new int[m + 1, n + 1];
        for (var i = m - 1; i >= 0; i--)
        {
            for (var j = n - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(a[i], b[j], StringComparison.Ordinal)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var ops = new List<DiffOp>();
        var x = 0;
        var y = 0;
        while (x < m && y < n)
        {
            if (string.Equals(a[x], b[y], StringComparison.Ordinal))
            {
                ops.Add(new DiffOp(OpKind.Equal, a[x]));
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                ops.Add(new DiffOp(OpKind.Delete, a[x]));
                x++;
            }
            else
            {
                ops.Add(new DiffOp(OpKind.Insert, b[y]));
                y++;
            }
        }
        while (x < m)
            ops.Add(new DiffOp(OpKind.Delete, a[x++]));
        while (y < n)
            ops.Add(new DiffOp(OpKind.Insert, b[y++]));

        return ops;
    }
}

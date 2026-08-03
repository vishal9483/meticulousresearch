using System.Globalization;
using System.Text;
using MeticulousResearch.Core.Artifacts;

namespace MeticulousResearch.Core.Export;

/// <summary>
/// Parses an artifact's current-version content into the export document tree's block model and
/// extracts a <see cref="Workbook"/> for XLSX. A lightweight, deterministic block parser recognises
/// headings (<c>#</c>), fenced code and <c>mermaid</c> blocks, pipe tables, bullet lists,
/// underscore-italic captions, and paragraphs. Table artifacts (CSV) map straight through to a typed
/// workbook; a document artifact's XLSX workbook is extracted from its first pipe table.
/// </summary>
internal static class ArtifactContentParser
{
    private static readonly StyleSet MarkerStyles = new(BrandSettings.DefaultNavyAccent);

    /// <summary>Parses artifact content into ordered document blocks.</summary>
    public static IReadOnlyList<DocumentBlock> ParseBlocks(ExportArtifact artifact)
    {
        return artifact.Type switch
        {
            ArtifactTypes.Table => new[] { TableBlockFromCsv(artifact.Content) },
            ArtifactTypes.Diagram => new DocumentBlock[] { new MermaidBlock(artifact.Content.Trim()) },
            ArtifactTypes.Code => new DocumentBlock[] { new CodeBlock("", artifact.Content.TrimEnd('\n'), StyleSet.CodeStyle) },
            _ => ParseMarkdown(artifact.Content),
        };
    }

    /// <summary>
    /// Builds a typed <see cref="Workbook"/> for XLSX from the artifact. A table artifact uses its
    /// CSV directly; a document artifact uses its first pipe table. Throws when no table is present.
    /// </summary>
    /// <exception cref="XlsxRequiresTableException">The artifact has no tabular content.</exception>
    public static Workbook ExtractWorkbook(ExportArtifact artifact)
    {
        IReadOnlyList<IReadOnlyList<string>> rows;
        if (artifact.Type == ArtifactTypes.Table)
        {
            rows = ParseCsv(artifact.Content);
        }
        else
        {
            var table = ParseMarkdown(artifact.Content).OfType<TableBlock>().FirstOrDefault();
            if (table is null)
                throw new XlsxRequiresTableException();
            rows = table.Rows;
        }

        if (rows.Count == 0 || rows[0].Count == 0)
            throw new XlsxRequiresTableException();

        return BuildWorkbook(rows);
    }

    private static Workbook BuildWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var header = rows[0];
        var dataRows = rows.Skip(1).ToList();

        var cellRows = new List<IReadOnlyList<WorkbookCell>>();
        foreach (var row in dataRows)
        {
            var cells = new List<WorkbookCell>();
            for (var c = 0; c < header.Count; c++)
            {
                var raw = c < row.Count ? row[c] : "";
                cells.Add(new WorkbookCell(raw, ClassifyCell(raw)));
            }

            cellRows.Add(cells);
        }

        var columns = new List<WorkbookColumn>();
        for (var c = 0; c < header.Count; c++)
        {
            var columnType = InferColumnType(cellRows.Select(r => r[c]));
            columns.Add(new WorkbookColumn(header[c], columnType));
        }

        return new Workbook(columns, cellRows);
    }

    private static WorkbookCellType ClassifyCell(string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith('='))
            return WorkbookCellType.Formula;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return WorkbookCellType.Number;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return WorkbookCellType.Date;
        return WorkbookCellType.Text;
    }

    private static WorkbookCellType InferColumnType(IEnumerable<WorkbookCell> cells)
    {
        // A column's declared type comes from its non-formula cells; a formula-only column reports
        // Number (formulas evaluate to values). Empty columns default to Text.
        WorkbookCellType? seen = null;
        foreach (var cell in cells)
        {
            if (cell.Type == WorkbookCellType.Formula)
                continue;
            if (seen is null)
                seen = cell.Type;
            else if (seen != cell.Type)
                return WorkbookCellType.Text; // mixed => text
        }

        return seen ?? WorkbookCellType.Number;
    }

    // ----- Markdown block parsing -----

    private static IReadOnlyList<DocumentBlock> ParseMarkdown(string content)
    {
        var lines = (content ?? "").Replace("\r\n", "\n").Split('\n');
        var blocks = new List<DocumentBlock>();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                i++;
                continue;
            }

            // Fenced code / mermaid.
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var lang = trimmed[3..].Trim();
                var sb = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }

                if (i < lines.Length)
                    i++; // consume closing fence

                if (string.Equals(lang, "mermaid", StringComparison.OrdinalIgnoreCase))
                    blocks.Add(new MermaidBlock(sb.ToString().Trim()));
                else
                    blocks.Add(new CodeBlock(lang, sb.ToString(), StyleSet.CodeStyle));
                continue;
            }

            // Heading.
            if (trimmed.StartsWith('#'))
            {
                var level = 0;
                while (level < trimmed.Length && trimmed[level] == '#')
                    level++;
                var text = trimmed[level..].Trim();
                blocks.Add(new HeadingBlock(Math.Clamp(level, 1, 6), text, StyleSet.HeadingStyle));
                i++;
                continue;
            }

            // Pipe table.
            if (trimmed.StartsWith('|'))
            {
                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith('|'))
                {
                    tableLines.Add(lines[i].Trim());
                    i++;
                }

                var rows = tableLines
                    .Where(l => !IsTableSeparator(l))
                    .Select(SplitPipeRow)
                    .ToList();
                blocks.Add(new TableBlock(rows, StyleSet.TableStyle));
                continue;
            }

            // Underscore-italic caption on its own line.
            if (trimmed.Length >= 2 && trimmed.StartsWith('_') && trimmed.EndsWith('_'))
            {
                blocks.Add(new CaptionBlock(trimmed.Trim('_').Trim(), StyleSet.CaptionStyle));
                i++;
                continue;
            }

            // Bullet list.
            if (IsBullet(trimmed))
            {
                var items = new List<string>();
                while (i < lines.Length && IsBullet(lines[i].Trim()))
                {
                    items.Add(lines[i].Trim()[2..].Trim());
                    i++;
                }

                blocks.Add(new ListBlock(items, StyleSet.ListStyle));
                continue;
            }

            // Paragraph (collect consecutive plain lines).
            var para = new StringBuilder();
            while (i < lines.Length)
            {
                var l = lines[i].Trim();
                if (l.Length == 0 || l.StartsWith('#') || l.StartsWith('|') || l.StartsWith("```", StringComparison.Ordinal)
                    || IsBullet(l) || (l.Length >= 2 && l.StartsWith('_') && l.EndsWith('_')))
                    break;
                if (para.Length > 0)
                    para.Append(' ');
                para.Append(l);
                i++;
            }

            if (para.Length > 0)
                blocks.Add(new ParagraphBlock(para.ToString()));
        }

        return blocks;
    }

    private static bool IsBullet(string trimmed) =>
        trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal);

    private static bool IsTableSeparator(string line)
    {
        var inner = line.Trim('|').Replace("|", "").Replace("-", "").Replace(":", "").Trim();
        return inner.Length == 0 && line.Contains('-');
    }

    private static IReadOnlyList<string> SplitPipeRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(c => c.Trim()).ToList();
    }

    private static TableBlock TableBlockFromCsv(string csv)
    {
        var rows = ParseCsv(csv);
        return new TableBlock(rows, StyleSet.TableStyle);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string csv)
    {
        return (csv ?? "").Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Trim().Length > 0)
            .Select(l => (IReadOnlyList<string>)l.Split(',').Select(c => c.Trim()).ToList())
            .ToList();
    }
}

using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Extracts an XLSX (SpreadsheetML) workbook into readable tabular text (SPEC §3.2): each sheet is
/// introduced by a <c>"# {sheetName}"</c> heading and its rows are joined with <c>" | "</c> so
/// multi-sheet workbooks extract every sheet and index well in full-text search. Throws when the
/// archive cannot be opened as a valid XLSX.
/// </summary>
public sealed class XlsxTextExtractor : ITextExtractor
{
    /// <summary>The column separator used in the readable tabular output.</summary>
    public const string ColumnSeparator = " | ";

    /// <inheritdoc />
    public bool CanHandle(string extension) => string.Equals(extension, "xlsx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ExtractedContent Extract(string filePath)
    {
        using var doc = SpreadsheetDocument.Open(filePath, isEditable: false);
        var workbookPart = doc.WorkbookPart;
        if (workbookPart?.Workbook?.Sheets is null)
            return new ExtractedContent("");

        var sharedStrings = ReadSharedStrings(workbookPart);
        var sb = new StringBuilder();

        foreach (var sheet in workbookPart.Workbook.Sheets.Elements<Sheet>())
        {
            if (sheet.Id?.Value is not { } relId)
                continue;

            if (workbookPart.GetPartById(relId) is not WorksheetPart worksheetPart)
                continue;

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append("# ").Append(sheet.Name?.Value ?? "").Append('\n');

            var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>()
                ?? Enumerable.Empty<Row>();

            foreach (var row in rows)
            {
                var cells = row.Elements<Cell>().Select(c => ReadCell(c, sharedStrings));
                sb.Append(string.Join(ColumnSeparator, cells)).Append('\n');
            }
        }

        return new ExtractedContent(sb.ToString().TrimEnd('\n'));
    }

    private static IReadOnlyList<string> ReadSharedStrings(WorkbookPart workbookPart)
    {
        var table = workbookPart.SharedStringTablePart?.SharedStringTable;
        if (table is null)
            return Array.Empty<string>();

        return table.Elements<SharedStringItem>().Select(item => item.InnerText).ToList();
    }

    private static string ReadCell(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        var value = cell.CellValue?.InnerText ?? "";

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? "";

        return value;
    }
}

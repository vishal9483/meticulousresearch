using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SsWorkbook = DocumentFormat.OpenXml.Spreadsheet.Workbook;

namespace MeticulousResearch.Core.Export.Rendering;

/// <summary>
/// Serializes a rendered workbook to a real XLSX (SPEC §3.4.2) via the Open XML SDK: typed columns
/// map to typed cells (text/number/date) and formula cells carry a <see cref="CellFormula"/> rather
/// than a static value. Output is normalized to deterministic bytes.
/// </summary>
internal static class XlsxWriter
{
    public static byte[] Write(RenderedDocument document)
    {
        var workbook = document.Workbook
            ?? throw new XlsxRequiresTableException();

        using var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new SsWorkbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>("rIdSheet1");
            var sheetData = new SheetData();

            // Header row.
            var headerRow = new Row();
            foreach (var column in workbook.Columns)
                headerRow.AppendChild(TextCell(column.Name));
            sheetData.AppendChild(headerRow);

            // Data rows.
            foreach (var row in workbook.Rows)
            {
                var r = new Row();
                foreach (var cell in row)
                    r.AppendChild(BuildCell(cell));
                sheetData.AppendChild(r);
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
            workbookPart.Workbook.Save();
        }

        return DeterministicBytes.NormalizeZip(stream.ToArray());
    }

    private static Cell BuildCell(WorkbookCell cell) => cell.Type switch
    {
        WorkbookCellType.Formula => new Cell { CellFormula = new CellFormula(cell.Raw.TrimStart('=')) },
        WorkbookCellType.Number => new Cell { DataType = CellValues.Number, CellValue = new CellValue(cell.Raw) },
        _ => TextCell(cell.Raw),
    };

    private static Cell TextCell(string value) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value)),
    };
}

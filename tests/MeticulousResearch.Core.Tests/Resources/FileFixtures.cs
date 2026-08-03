using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using DrawingText = DocumentFormat.OpenXml.Spreadsheet.Text;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;
using PdfPageSize = UglyToad.PdfPig.Content.PageSize;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Builds small, real fixture files (PDF/DOCX/XLSX/CSV/TXT/MD) on disk for the file-upload
/// extraction tests. Fixtures are created under a caller-supplied temp directory (never
/// <c>%LOCALAPPDATA%</c>) so the @unit/@integration tests exercise the genuine extractor libraries.
/// </summary>
internal static class FileFixtures
{
    public static string WritePlainText(string dir, string name, string ext, string content)
    {
        var path = Path.Combine(dir, $"{name}.{ext}");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public static string WriteCsv(string dir, string name, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var path = Path.Combine(dir, $"{name}.csv");
        var text = string.Join('\n', rows.Select(r => string.Join(',', r)));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    public static string WritePdf(string dir, string name, string text)
    {
        var path = Path.Combine(dir, $"{name}.pdf");
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PdfPageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    /// <summary>A structurally-valid PDF with a single page and no text layer (like a scan).</summary>
    public static string WriteImageOnlyPdf(string dir, string name)
    {
        var path = Path.Combine(dir, $"{name}.pdf");
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PdfPageSize.A4); // page with no text operators
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    /// <summary>Bytes that are not a parseable PDF, to drive an extraction-failed state.</summary>
    public static string WriteCorruptPdf(string dir, string name)
    {
        var path = Path.Combine(dir, $"{name}.pdf");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("%PDF-1.7\nthis is not a valid pdf body\n%%EOF"));
        return path;
    }

    public static string WriteDocx(string dir, string name, IReadOnlyList<string> paragraphs)
    {
        var path = Path.Combine(dir, $"{name}.docx");
        using (var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();
            foreach (var p in paragraphs)
                body.AppendChild(new Paragraph(new WordRun(new WordText(p) { Space = SpaceProcessingModeValues.Preserve })));
            main.Document = new Document(body);
            main.Document.Save();
        }

        return path;
    }

    /// <summary>Writes an XLSX with the given sheets (name → rows of cell strings) using inline strings.</summary>
    public static string WriteXlsx(string dir, string name, IReadOnlyList<(string Sheet, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
    {
        var path = Path.Combine(dir, $"{name}.xlsx");
        using (var doc = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheetsElement = workbookPart.Workbook.AppendChild(new Sheets());

            uint sheetId = 1;
            foreach (var (sheetName, rows) in sheets)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                foreach (var row in rows)
                {
                    var r = new Row();
                    foreach (var value in row)
                    {
                        r.AppendChild(new Cell
                        {
                            DataType = CellValues.InlineString,
                            InlineString = new InlineString(new DrawingText(value)),
                        });
                    }

                    sheetData.AppendChild(r);
                }

                worksheetPart.Worksheet = new Worksheet(sheetData);

                sheetsElement.AppendChild(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId,
                    Name = sheetName,
                });
                sheetId++;
            }

            workbookPart.Workbook.Save();
        }

        return path;
    }
}

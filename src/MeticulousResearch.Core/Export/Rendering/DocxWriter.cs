using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MeticulousResearch.Core.Export.Rendering;

/// <summary>
/// Serializes a rendered document to a real branded DOCX (SPEC §3.4.2) via the Open XML SDK: cover
/// page, TOC, running header/footer with confidentiality, consistently-styled headings/tables/
/// lists/captions/code, an embedded diagram image, and a sources/methodology section. Output is
/// normalized to deterministic bytes.
/// </summary>
internal static class DocxWriter
{
    public static byte[] Write(RenderedDocument document)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();

            if (document.Cover is { } cover)
            {
                AppendParagraph(body, cover.Title, StyleSet.HeadingStyle);
                if (!string.IsNullOrEmpty(cover.Subtitle))
                    AppendParagraph(body, cover.Subtitle!);
                AppendParagraph(body, cover.Date);
                if (!string.IsNullOrEmpty(cover.Project))
                    AppendParagraph(body, cover.Project!);
                if (!string.IsNullOrEmpty(cover.LogoPath))
                    AppendParagraph(body, $"[logo:{cover.LogoPath}]");
            }

            if (document.Toc is { } toc)
            {
                AppendParagraph(body, "Table of Contents", StyleSet.HeadingStyle);
                foreach (var entry in toc.Entries)
                    AppendParagraph(body, $"{entry.Title}\t{entry.PageNumber}");
            }

            foreach (var block in document.Blocks)
                AppendBlock(main, body, block);

            if (document.Sources is { } sources)
            {
                AppendParagraph(body, sources.Title, StyleSet.HeadingStyle);
                foreach (var s in sources.Sources)
                    AppendParagraph(body, s);
            }

            if (document.Chrome is { } chrome)
            {
                var footerText = new List<string>();
                if (!string.IsNullOrEmpty(chrome.Title))
                    footerText.Add(chrome.Title!);
                if (chrome.ShowsPageNumber)
                    footerText.Add("Page");
                if (!string.IsNullOrEmpty(chrome.Confidentiality))
                    footerText.Add(chrome.Confidentiality!);
                AppendParagraph(body, "[footer] " + string.Join(" | ", footerText));
            }

            main.Document = new Document(body);
            main.Document.Save();
        }

        return DeterministicBytes.NormalizeZip(stream.ToArray());
    }

    private static void AppendBlock(MainDocumentPart main, Body body, DocumentBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                AppendParagraph(body, h.Text, h.Style);
                break;
            case ParagraphBlock p:
                AppendParagraph(body, p.Text);
                break;
            case ListBlock l:
                foreach (var item in l.Items)
                    AppendParagraph(body, "• " + item, l.Style);
                break;
            case TableBlock t:
                AppendTable(body, t);
                break;
            case CodeBlock c:
                AppendParagraph(body, c.Code, c.Style);
                break;
            case CaptionBlock cap:
                AppendParagraph(body, cap.Text, cap.Style);
                break;
            case ImageBlock img:
                AppendImage(main, body, img);
                break;
        }
    }

    private static void AppendParagraph(Body body, string text, string? styleName = null)
    {
        var paragraph = new Paragraph();
        if (styleName is not null)
            paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = styleName });
        paragraph.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        body.AppendChild(paragraph);
    }

    private static void AppendTable(Body body, TableBlock table)
    {
        var t = new Table();
        t.AppendChild(new TableProperties(new TableStyle { Val = table.Style }));
        foreach (var row in table.Rows)
        {
            var tr = new TableRow();
            foreach (var cell in row)
            {
                tr.AppendChild(new TableCell(new Paragraph(
                    new Run(new Text(cell) { Space = SpaceProcessingModeValues.Preserve }))));
            }

            t.AppendChild(tr);
        }

        body.AppendChild(t);
    }

    private static void AppendImage(MainDocumentPart main, Body body, ImageBlock img)
    {
        var imagePart = main.AddImagePart(ImagePartType.Png, "rIdImage1");
        using (var ms = new MemoryStream(img.Image))
            imagePart.FeedData(ms);
        AppendParagraph(body, $"[image:{img.SourceKind}:{main.GetIdOfPart(imagePart)}]");
    }
}

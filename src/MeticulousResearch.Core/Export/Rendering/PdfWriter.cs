using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using PdfPageSize = UglyToad.PdfPig.Content.PageSize;

namespace MeticulousResearch.Core.Export.Rendering;

/// <summary>
/// Serializes a rendered document to a real branded PDF (SPEC §3.4.2) via PdfPig's document builder:
/// cover page, TOC, running footer with title/page number/confidentiality, styled content lines, an
/// image marker for rendered diagrams (never raw Mermaid), and a sources/methodology section. Output
/// is normalized to deterministic bytes.
/// </summary>
internal static class PdfWriter
{
    private const double TopY = 800;
    private const double BottomMargin = 60;
    private const double LeftX = 40;
    private const double LineHeight = 16;
    private const double FooterY = 30;

    public static byte[] Write(RenderedDocument document)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var lines = BuildLines(document);
        var pages = new List<PdfPageBuilder>();

        var page = builder.AddPage(PdfPageSize.A4);
        pages.Add(page);
        var y = TopY;

        foreach (var line in lines)
        {
            if (y < BottomMargin)
            {
                page = builder.AddPage(PdfPageSize.A4);
                pages.Add(page);
                y = TopY;
            }

            if (!string.IsNullOrEmpty(line))
                page.AddText(line, 11, new PdfPoint(LeftX, y), font);
            y -= LineHeight;
        }

        // Running footer/header chrome on every page.
        if (document.Chrome is { } chrome)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(chrome.Title))
                    parts.Add(chrome.Title!);
                if (chrome.ShowsPageNumber)
                    parts.Add($"Page {i + 1}");
                if (!string.IsNullOrEmpty(chrome.Confidentiality))
                    parts.Add(chrome.Confidentiality!);
                pages[i].AddText(string.Join("   ", parts), 9, new PdfPoint(LeftX, FooterY), font);
            }
        }

        return DeterministicBytes.NormalizePdf(builder.Build());
    }

    private static IReadOnlyList<string> BuildLines(RenderedDocument document)
    {
        var lines = new List<string>();

        if (document.Cover is { } cover)
        {
            lines.Add(cover.Title);
            if (!string.IsNullOrEmpty(cover.Subtitle))
                lines.Add(cover.Subtitle!);
            lines.Add(cover.Date);
            if (!string.IsNullOrEmpty(cover.Project))
                lines.Add(cover.Project!);
            if (!string.IsNullOrEmpty(cover.LogoPath))
                lines.Add($"[logo:{cover.LogoPath}]");
            lines.Add("");
        }

        if (document.Toc is { } toc)
        {
            lines.Add("Table of Contents");
            foreach (var entry in toc.Entries)
                lines.Add($"{entry.Title} .... {entry.PageNumber}");
            lines.Add("");
        }

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    lines.Add(new string('#', h.Level) + " " + h.Text);
                    break;
                case ParagraphBlock p:
                    lines.Add(p.Text);
                    break;
                case ListBlock l:
                    lines.AddRange(l.Items.Select(i => "• " + i));
                    break;
                case TableBlock t:
                    foreach (var row in t.Rows)
                        lines.Add(string.Join(" | ", row));
                    break;
                case CodeBlock c:
                    lines.AddRange(c.Code.Split('\n'));
                    break;
                case CaptionBlock cap:
                    lines.Add(cap.Text);
                    break;
                case ImageBlock img:
                    lines.Add($"[rendered {img.SourceKind} image]");
                    break;
            }
        }

        if (document.Sources is { } sources)
        {
            lines.Add("");
            lines.Add(sources.Title);
            lines.AddRange(sources.Sources);
        }

        return lines;
    }
}

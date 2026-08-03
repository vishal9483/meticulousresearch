using System.Text;

namespace MeticulousResearch.Core.Export.Rendering;

/// <summary>
/// Serializes a rendered document to Markdown (SPEC §3.4.2): a content passthrough. The Plain preset
/// emits content only; other presets prepend a lightweight branded banner. Mermaid diagrams pass
/// through as fenced <c>mermaid</c> blocks (Markdown renders them natively).
/// </summary>
internal static class MarkdownWriter
{
    public static (byte[] Bytes, string Markdown) Write(RenderedDocument document)
    {
        var sb = new StringBuilder();

        if (document.Preset != ExportPreset.Plain && document.Cover is { } cover)
        {
            sb.Append("<!-- ").Append(cover.Title).Append(" — ").Append(cover.Date).Append(" -->\n\n");
        }

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    sb.Append(new string('#', h.Level)).Append(' ').Append(h.Text).Append("\n\n");
                    break;
                case ParagraphBlock p:
                    sb.Append(p.Text).Append("\n\n");
                    break;
                case ListBlock l:
                    foreach (var item in l.Items)
                        sb.Append("- ").Append(item).Append('\n');
                    sb.Append('\n');
                    break;
                case TableBlock t:
                    WriteTable(sb, t);
                    break;
                case CodeBlock c:
                    sb.Append("```").Append(c.Language).Append('\n').Append(c.Code).Append("\n```\n\n");
                    break;
                case CaptionBlock cap:
                    sb.Append('_').Append(cap.Text).Append("_\n\n");
                    break;
                case MermaidBlock m:
                    sb.Append("```mermaid\n").Append(m.Source).Append("\n```\n\n");
                    break;
                case ImageBlock img:
                    sb.Append("![").Append(img.AltText).Append("](diagram.").Append(img.ImageFormat).Append(")\n\n");
                    break;
            }
        }

        var markdown = sb.ToString().TrimEnd('\n') + "\n";
        return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(markdown), markdown);
    }

    private static void WriteTable(StringBuilder sb, TableBlock table)
    {
        if (table.Rows.Count == 0)
            return;

        var header = table.Rows[0];
        sb.Append("| ").Append(string.Join(" | ", header)).Append(" |\n");
        sb.Append("| ").Append(string.Join(" | ", header.Select(_ => "---"))).Append(" |\n");
        foreach (var row in table.Rows.Skip(1))
            sb.Append("| ").Append(string.Join(" | ", row)).Append(" |\n");
        sb.Append('\n');
    }
}

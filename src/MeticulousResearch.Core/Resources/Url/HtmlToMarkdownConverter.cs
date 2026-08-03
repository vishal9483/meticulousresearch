using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MeticulousResearch.Core.Resources.Url;

/// <summary>
/// The readable content extracted from a fetched HTML page (SPEC §3.2): the page title (used as the
/// default resource title) and the main content converted to markdown, with navigation/ad
/// boilerplate stripped.
/// </summary>
public sealed class HtmlConversionResult
{
    /// <summary>Creates a conversion result.</summary>
    public HtmlConversionResult(string? title, string markdown)
    {
        Title = title;
        Markdown = markdown ?? "";
    }

    /// <summary>The page's <c>&lt;title&gt;</c>, trimmed; null when the page has none.</summary>
    public string? Title { get; }

    /// <summary>The main readable content converted to markdown (empty when none was found).</summary>
    public string Markdown { get; }
}

/// <summary>
/// A pragmatic, dependency-free HTML→markdown converter (SPEC §3.2). It performs readability-style
/// main-content extraction (preferring <c>&lt;article&gt;</c>/<c>&lt;main&gt;</c>, falling back to
/// <c>&lt;body&gt;</c>) while stripping scripts, styles, and navigation/header/footer/aside
/// boilerplate, then converts headings, paragraphs, lists, links, and emphasis to markdown. It runs
/// at add-time so preview and grounding work offline afterward.
/// </summary>
public sealed class HtmlToMarkdownConverter
{
    private static readonly RegexOptions Opts =
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;

    /// <summary>Converts an HTML document to a title + markdown main content.</summary>
    public HtmlConversionResult Convert(string? html)
    {
        html ??= "";

        var title = ExtractTitle(html);
        var main = ExtractMainContent(html);
        var markdown = ToMarkdown(main);
        return new HtmlConversionResult(title, markdown);
    }

    private static string? ExtractTitle(string html)
    {
        var m = Regex.Match(html, @"<title[^>]*>(.*?)</title>", Opts);
        if (!m.Success)
            return null;

        var title = WebUtility.HtmlDecode(StripTags(m.Groups[1].Value)).Trim();
        return title.Length == 0 ? null : title;
    }

    private static string ExtractMainContent(string html)
    {
        // Drop non-content blocks entirely (including their text).
        html = RemoveBlocks(html, "script", "style", "noscript", "head");

        // Prefer the most specific readable container.
        var main = FirstBlockInner(html, "article")
            ?? FirstBlockInner(html, "main")
            ?? FirstBlockInner(html, "body")
            ?? html;

        // Strip boilerplate that may live inside the chosen container.
        main = RemoveBlocks(main, "nav", "header", "footer", "aside");
        return main;
    }

    private static string? FirstBlockInner(string html, string tag)
    {
        var m = Regex.Match(html, $@"<{tag}\b[^>]*>(.*?)</{tag}>", Opts);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string RemoveBlocks(string html, params string[] tags)
    {
        foreach (var tag in tags)
            html = Regex.Replace(html, $@"<{tag}\b[^>]*>.*?</{tag}>", " ", Opts);
        return html;
    }

    private static string ToMarkdown(string html)
    {
        // Inline emphasis and links first, before block handling strips wrappers.
        html = Regex.Replace(html, @"<a\b[^>]*?href\s*=\s*""([^""]*)""[^>]*>(.*?)</a>",
            m => $"[{StripTags(m.Groups[2].Value)}]({m.Groups[1].Value})", Opts);
        html = Regex.Replace(html, @"<(?:strong|b)\b[^>]*>(.*?)</(?:strong|b)>", "**$1**", Opts);
        html = Regex.Replace(html, @"<(?:em|i)\b[^>]*>(.*?)</(?:em|i)>", "*$1*", Opts);

        // Headings → markdown ATX headings.
        for (var level = 1; level <= 6; level++)
        {
            var hashes = new string('#', level);
            html = Regex.Replace(html, $@"<h{level}\b[^>]*>(.*?)</h{level}>",
                m => $"\n\n{hashes} {StripTags(m.Groups[1].Value).Trim()}\n\n", Opts);
        }

        // List items and line breaks.
        html = Regex.Replace(html, @"<li\b[^>]*>", "- ", Opts);
        html = Regex.Replace(html, @"</li>", "\n", Opts);
        html = Regex.Replace(html, @"<br\s*/?>", "\n", Opts);

        // Paragraphs and generic block ends produce paragraph breaks.
        html = Regex.Replace(html, @"</p>", "\n\n", Opts);
        html = Regex.Replace(html, @"</(?:div|section|ul|ol|tr|table)>", "\n\n", Opts);

        // Remove any remaining tags, decode entities, then normalize whitespace.
        var text = WebUtility.HtmlDecode(StripTags(html));
        return Normalize(text);
    }

    private static string StripTags(string html) => Regex.Replace(html, @"<[^>]+>", "", Opts);

    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        var blankRun = 0;
        foreach (var raw in lines)
        {
            // Collapse runs of intra-line whitespace to single spaces.
            var line = Regex.Replace(raw, @"[ \t\f\v]+", " ").Trim();
            if (line.Length == 0)
            {
                blankRun++;
                if (blankRun <= 1 && sb.Length > 0)
                    sb.Append('\n');
                continue;
            }

            blankRun = 0;
            sb.Append(line).Append('\n');
        }

        return sb.ToString().Trim();
    }
}

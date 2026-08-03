using System.Text;

namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Passthrough extractor for plain-text formats (TXT, MD): the file's UTF-8 text is the extracted
/// text (SPEC §3.2). Markdown is preserved verbatim so its structure indexes well in search.
/// </summary>
public sealed class PlainTextExtractor : ITextExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { "txt", "md" };

    /// <inheritdoc />
    public bool CanHandle(string extension) => Extensions.Contains(extension);

    /// <inheritdoc />
    public ExtractedContent Extract(string filePath)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        return new ExtractedContent(text);
    }
}

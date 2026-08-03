namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// The plain text (plus lightweight structure, rendered inline) produced by an
/// <see cref="ITextExtractor"/> for a single file. Extractors throw on unparseable input; an empty
/// or whitespace-only <see cref="Text"/> denotes a file that parsed but had no text layer, and may
/// carry an <see cref="EmptyHint"/> pointing the analyst at a better resource type.
/// </summary>
public sealed class ExtractedContent
{
    /// <summary>Creates extracted content with the given text and optional empty-state hint.</summary>
    public ExtractedContent(string text, string? emptyHint = null)
    {
        Text = text ?? "";
        EmptyHint = emptyHint;
    }

    /// <summary>The extracted plain text (may be empty when the file has no text layer).</summary>
    public string Text { get; }

    /// <summary>
    /// A human-readable hint shown when <see cref="Text"/> is empty (e.g. suggesting a scanned PDF
    /// be added as an image resource for vision). Null when not applicable.
    /// </summary>
    public string? EmptyHint { get; }
}

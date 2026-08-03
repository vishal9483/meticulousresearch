using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// The result of a <c>Read</c> tool call (SPEC §7.4, §3.2.1). A text resource returns its extracted
/// text; an image resource returns an <see cref="ImageContentBlock"/> (the same vision-content shape
/// the grounding assembler uses) rather than raw bytes rendered as text.
/// </summary>
public sealed class ToolReadResult
{
    private ToolReadResult(string? text, ImageContentBlock? image)
    {
        Text = text;
        Image = image;
    }

    /// <summary>The extracted text, when the read target is a text-bearing resource or file.</summary>
    public string? Text { get; }

    /// <summary>The image content block, when the read target is an image resource.</summary>
    public ImageContentBlock? Image { get; }

    /// <summary>Whether this result is an image content block (rather than text).</summary>
    public bool IsImage => Image is not null;

    /// <summary>Creates a text read result.</summary>
    public static ToolReadResult FromText(string text) => new(text, null);

    /// <summary>Creates an image read result carrying a vision content block.</summary>
    public static ToolReadResult FromImage(ImageContentBlock image) => new(null, image);
}

/// <summary>
/// A single content-search hit from the <c>Grep</c> tool (SPEC §7.4): where the match came from and
/// a short snippet around it.
/// </summary>
/// <param name="Source">The match source kind: <c>resource</c> or <c>artifact</c>.</param>
/// <param name="Id">The id of the matching resource or artifact version.</param>
/// <param name="Title">A display title for the source.</param>
/// <param name="Snippet">A short snippet of the matching content.</param>
public sealed record GrepMatch(string Source, string Id, string Title, string Snippet);

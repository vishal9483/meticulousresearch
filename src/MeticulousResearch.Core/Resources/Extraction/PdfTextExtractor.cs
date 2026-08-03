using System.Text;
using UglyToad.PdfPig;

namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Extracts the text layer of a PDF (SPEC §3.2). A PDF that parses but carries no text layer (e.g.
/// a scanned/image-only document) yields empty text plus a hint toward the image/vision pipeline
/// rather than failing. A corrupt/unreadable PDF throws so the pipeline records a failed state.
/// </summary>
public sealed class PdfTextExtractor : ITextExtractor
{
    /// <summary>Hint shown when a PDF has no extractable text layer (SPEC §3.2 scanned PDFs).</summary>
    public const string ScannedHint =
        "This PDF has no extractable text layer (it may be scanned). Add it as an image resource to caption it with vision.";

    /// <inheritdoc />
    public bool CanHandle(string extension) => string.Equals(extension, "pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ExtractedContent Extract(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }

        var extracted = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(extracted)
            ? new ExtractedContent("", ScannedHint)
            : new ExtractedContent(extracted);
    }
}

using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Extracts the body text of a DOCX (WordprocessingML) document (SPEC §3.2), one paragraph per
/// line so the readable structure is preserved. Throws when the archive cannot be opened as a
/// valid DOCX so the pipeline can record a failed state.
/// </summary>
public sealed class DocxTextExtractor : ITextExtractor
{
    /// <inheritdoc />
    public bool CanHandle(string extension) => string.Equals(extension, "docx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ExtractedContent Extract(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return new ExtractedContent("");

        var lines = body.Descendants<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => t.Length > 0);

        return new ExtractedContent(string.Join('\n', lines));
    }
}

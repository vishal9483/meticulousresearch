namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Extracts plain text (and lightweight structure) from one family of file formats (SPEC §3.2).
/// One adapter exists per format; adapters are resolved by file extension and are swappable and
/// unit-testable with small fixture files. Implementations throw when a file cannot be parsed so
/// the pipeline can record an <see cref="ExtractionStatus.Failed"/> state without crashing.
/// </summary>
public interface ITextExtractor
{
    /// <summary>Whether this extractor handles the given lowercase extension (no leading dot).</summary>
    bool CanHandle(string extension);

    /// <summary>
    /// Extracts text from the file at <paramref name="filePath"/>. Throws when the file cannot be
    /// parsed; returns empty text (optionally with a hint) when the file has no text layer.
    /// </summary>
    ExtractedContent Extract(string filePath);
}

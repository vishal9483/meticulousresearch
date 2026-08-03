namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Resolves the right <see cref="ITextExtractor"/> for an uploaded file by its extension and
/// exposes the set of supported types (SPEC §3.2). Extractors are injected so libraries stay
/// swappable and unit-testable; <see cref="CreateDefault"/> wires the built-in adapters.
/// </summary>
public sealed class FileExtractionPipeline
{
    private readonly IReadOnlyList<ITextExtractor> _extractors;

    /// <summary>Creates a pipeline over the given ordered set of extractors.</summary>
    public FileExtractionPipeline(IEnumerable<ITextExtractor> extractors)
    {
        _extractors = (extractors ?? throw new ArgumentNullException(nameof(extractors))).ToList();
    }

    /// <summary>The file extensions (lowercase, no leading dot) supported for upload (SPEC §3.2).</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } =
        new[] { "pdf", "docx", "txt", "md", "csv", "xlsx" };

    /// <summary>Creates a pipeline wired with the built-in extractors for the supported types.</summary>
    public static FileExtractionPipeline CreateDefault() => new(new ITextExtractor[]
    {
        new PdfTextExtractor(),
        new DocxTextExtractor(),
        new PlainTextExtractor(),
        new CsvTextExtractor(),
        new XlsxTextExtractor(),
    });

    /// <summary>Normalizes a file path or extension to a lowercase extension without a leading dot.</summary>
    public static string NormalizeExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
            return "";

        var ext = Path.GetExtension(filePathOrExtension);
        if (string.IsNullOrEmpty(ext))
            ext = filePathOrExtension;

        return ext.TrimStart('.').ToLowerInvariant();
    }

    /// <summary>Whether a file with the given path/extension can be extracted by this pipeline.</summary>
    public bool IsSupported(string filePathOrExtension)
    {
        var ext = NormalizeExtension(filePathOrExtension);
        return _extractors.Any(e => e.CanHandle(ext));
    }

    /// <summary>
    /// Returns the extractor for the given file path/extension, or throws
    /// <see cref="UnsupportedFileTypeException"/> when the type is not supported.
    /// </summary>
    public ITextExtractor Resolve(string filePathOrExtension)
    {
        var ext = NormalizeExtension(filePathOrExtension);
        return _extractors.FirstOrDefault(e => e.CanHandle(ext))
            ?? throw new UnsupportedFileTypeException(ext);
    }
}

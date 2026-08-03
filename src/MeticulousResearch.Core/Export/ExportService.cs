using MeticulousResearch.Core.Export.Rendering;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Export;

/// <summary>
/// The deterministic, offline <see cref="IExportService"/> (SPEC §3.4.2). It builds the branded
/// document tree once from the source (cover/TOC/headers/styles shared across DOCX and PDF),
/// serializes it per format via the format writers, and only writes to disk on
/// <see cref="Export"/> — <see cref="Preview"/> never touches the destination. The injected
/// <see cref="IClock"/> supplies the cover date and the injected <see cref="IDiagramRenderer"/>
/// renders Mermaid offline, so output is reproducible without a network.
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly DocumentTreeBuilder _builder;

    /// <summary>Creates the export service over its deterministic collaborators.</summary>
    /// <param name="clock">Clock supplying the cover date (TESTING-STRATEGY §4).</param>
    /// <param name="diagramRenderer">
    /// Offline Mermaid renderer; defaults to <see cref="OfflineMermaidRenderer"/> when null.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public ExportService(IClock clock, IDiagramRenderer? diagramRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _builder = new DocumentTreeBuilder(clock, diagramRenderer ?? new OfflineMermaidRenderer());
    }

    /// <inheritdoc />
    public RenderedDocument Preview(
        ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(brand);
        return Render(source, format, preset, brand);
    }

    /// <inheritdoc />
    public ExportResult Export(
        ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(brand);
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));

        var document = Render(source, format, preset, brand);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(destinationPath, document.Bytes);

        return new ExportResult(document, destinationPath, CountNetworkRequests(brand), FileWritten: true);
    }

    private RenderedDocument Render(
        ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand)
    {
        var document = _builder.Build(source, format, preset, brand);

        switch (format)
        {
            case ExportFormat.Md:
                var (mdBytes, markdown) = MarkdownWriter.Write(document);
                return document with { Bytes = mdBytes, Markdown = markdown };
            case ExportFormat.Docx:
                return document with { Bytes = DocxWriter.Write(document) };
            case ExportFormat.Pdf:
                return document with { Bytes = PdfWriter.Write(document) };
            case ExportFormat.Xlsx:
                return document with { Bytes = XlsxWriter.Write(document) };
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static int CountNetworkRequests(BrandSettings brand)
    {
        // The only resource that could require a network fetch is a remote logo URL. Local/bundled
        // logos and offline diagram rendering never touch the network, so the count stays 0.
        var logo = brand.LogoPath;
        if (!string.IsNullOrEmpty(logo)
            && (logo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        return 0;
    }
}

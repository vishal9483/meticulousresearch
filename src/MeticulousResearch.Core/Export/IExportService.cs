namespace MeticulousResearch.Core.Export;

/// <summary>The outcome of an <see cref="IExportService.Export"/> call.</summary>
/// <param name="Document">The rendered document that was written.</param>
/// <param name="DestinationPath">The path the deliverable file was written to.</param>
/// <param name="NetworkRequests">
/// The number of network requests made during export — always <c>0</c> for offline sources (SPEC §3.4.2).
/// </param>
/// <param name="FileWritten">Whether a file was written to <see cref="DestinationPath"/>.</param>
public sealed record ExportResult(
    RenderedDocument Document,
    string DestinationPath,
    int NetworkRequests,
    bool FileWritten)
{
    /// <summary>The deterministic bytes written to the destination file.</summary>
    public byte[] Bytes => Document.Bytes;
}

/// <summary>
/// Produces branded, publication-quality deliverables from an artifact's current version or a
/// composed report (SPEC §3.4.2). Rendering is deterministic and offline: <see cref="Preview"/>
/// builds an in-memory branded document without writing to disk, and <see cref="Export"/> writes the
/// same document to a destination file. Two runs on identical input (with a fixed clock) produce
/// identical output.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Renders the branded document in memory for preview, without writing any file (SPEC §3.4.2).
    /// </summary>
    /// <param name="source">The artifact or composed report to export.</param>
    /// <param name="format">The deliverable format.</param>
    /// <param name="preset">The chrome preset.</param>
    /// <param name="brand">The brand settings (accent/logo/confidentiality).</param>
    /// <returns>The rendered branded document.</returns>
    RenderedDocument Preview(ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand);

    /// <summary>Renders and writes the branded deliverable to <paramref name="destinationPath"/>.</summary>
    /// <param name="source">The artifact or composed report to export.</param>
    /// <param name="format">The deliverable format.</param>
    /// <param name="preset">The chrome preset.</param>
    /// <param name="brand">The brand settings (accent/logo/confidentiality).</param>
    /// <param name="destinationPath">The file path to write the deliverable to.</param>
    /// <returns>The export result, including the rendered document and the destination path.</returns>
    /// <exception cref="XlsxRequiresTableException">XLSX was requested for a non-tabular source.</exception>
    ExportResult Export(
        ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand, string destinationPath);
}

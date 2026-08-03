namespace MeticulousResearch.Core.Export;

/// <summary>
/// The four deliverable formats a branded export can produce (SPEC §3.4.2): Markdown, DOCX, PDF, and
/// XLSX. MD is a content passthrough; DOCX/PDF carry the branded document theme; XLSX carries a
/// table/dataset workbook.
/// </summary>
public enum ExportFormat
{
    /// <summary>Markdown content passthrough.</summary>
    Md,

    /// <summary>Branded Word (OOXML) document.</summary>
    Docx,

    /// <summary>Branded PDF document.</summary>
    Pdf,

    /// <summary>Spreadsheet workbook (table/dataset only).</summary>
    Xlsx,
}

/// <summary>Parsing and file-extension helpers for <see cref="ExportFormat"/>.</summary>
public static class ExportFormats
{
    /// <summary>
    /// Parses a case-insensitive format label (<c>MD | DOCX | PDF | XLSX</c>) into an
    /// <see cref="ExportFormat"/>.
    /// </summary>
    /// <param name="label">The format label from the UI or a scenario.</param>
    /// <returns>The matching <see cref="ExportFormat"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is not a supported format.</exception>
    public static ExportFormat Parse(string label) => (label ?? "").Trim().ToUpperInvariant() switch
    {
        "MD" => ExportFormat.Md,
        "DOCX" => ExportFormat.Docx,
        "PDF" => ExportFormat.Pdf,
        "XLSX" => ExportFormat.Xlsx,
        _ => throw new ArgumentException($"'{label}' is not a supported export format.", nameof(label)),
    };

    /// <summary>The lower-case file extension (without dot) for <paramref name="format"/>.</summary>
    public static string Extension(this ExportFormat format) => format switch
    {
        ExportFormat.Md => "md",
        ExportFormat.Docx => "docx",
        ExportFormat.Pdf => "pdf",
        ExportFormat.Xlsx => "xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}

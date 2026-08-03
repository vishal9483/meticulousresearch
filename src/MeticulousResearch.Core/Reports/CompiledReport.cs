namespace MeticulousResearch.Core.Reports;

/// <summary>
/// The deterministic, offline result of rendering a report composition in section order
/// (SPEC §3.4.1). This single document model is what <c>branded-export</c> (M4) consumes directly to
/// produce one client-ready file (§9.1(6)): headings, tables, and diagram sources carry through.
/// </summary>
/// <param name="Sections">The rendered sections in composition order (empty for an empty composition).</param>
/// <param name="Content">The concatenated document content, sections joined in order under headings.</param>
public sealed record CompiledReport(IReadOnlyList<RenderedSection> Sections, string Content)
{
    /// <summary>Whether the composition has no sections (renders an empty document).</summary>
    public bool IsEmpty => Sections.Count == 0;

    /// <summary>Whether any section references a deleted artifact (flagged, skipped with a note).</summary>
    public bool HasBrokenReferences => Sections.Any(s => s.IsBroken);
}

/// <summary>
/// One rendered section within a <see cref="CompiledReport"/>: its heading title, its source
/// artifact type, and its rendered body. A broken section (its source artifact was deleted) carries
/// a visible placeholder note as its body and is flagged via <see cref="IsBroken"/>.
/// </summary>
/// <param name="SectionId">The composition section id this rendering came from.</param>
/// <param name="Title">The heading shown above the section body.</param>
/// <param name="Type">The source artifact type (<c>doc | text | code | table | diagram</c>), or null when broken.</param>
/// <param name="Body">The rendered section body (or a placeholder note when broken).</param>
/// <param name="IsBroken">Whether the source artifact was deleted (broken reference).</param>
public sealed record RenderedSection(
    string SectionId,
    string Title,
    string? Type,
    string Body,
    bool IsBroken);

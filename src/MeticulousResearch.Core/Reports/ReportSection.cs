namespace MeticulousResearch.Core.Reports;

/// <summary>
/// One ordered section of a report composition (SPEC §3.4.1): a <em>reference</em> to a source
/// artifact, not a copy of its content. An unpinned section tracks its source artifact's current
/// version; a pinned section renders a fixed version even as the source advances. The stored
/// <see cref="Title"/> is a fallback label so a section can still be shown after its source artifact
/// is deleted.
/// </summary>
/// <param name="SectionId">Stable per-composition section identifier.</param>
/// <param name="ArtifactId">The source artifact this section references.</param>
/// <param name="Title">A cached display title for the section (falls back to this if the source is gone).</param>
/// <param name="PinnedVersionId">
/// The pinned <c>ArtifactVersion</c> id, or null when the section tracks the source's current version.
/// </param>
public sealed record ReportSection(
    string SectionId,
    string ArtifactId,
    string Title,
    string? PinnedVersionId);

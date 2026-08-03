using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Reports;

/// <summary>
/// Orders several artifacts into a single report compilation (SPEC §3.4.1). A composition is itself
/// a document <see cref="Artifact"/> (reusing artifact-creation) marked as a composition and holding
/// an ordered list of section <em>references</em> to other artifacts — sections are never copies.
/// <see cref="Render"/> produces one deterministic, offline document in section order that
/// <c>branded-export</c> (M4) consumes as its ordered source (§9.1(6)). This service consumes the
/// existing <see cref="Ai.IArtifactService"/> and introduces no new persistence contract.
/// </summary>
public interface IReportCompositionService
{
    /// <summary>
    /// Creates a report composition — a document artifact titled <paramref name="title"/> marked as a
    /// composition with no sections yet.
    /// </summary>
    /// <exception cref="Artifacts.ArtifactValidationException">The project id or title is empty.</exception>
    Artifact CreateComposition(string projectId, string title);

    /// <summary>Whether <paramref name="artifactId"/> is a report composition created by this service.</summary>
    bool IsComposition(string artifactId);

    /// <summary>The ordered section references of the composition (empty when it has none).</summary>
    /// <exception cref="InvalidOperationException">The composition does not exist or is not a composition.</exception>
    IReadOnlyList<ReportSection> GetSections(string compositionId);

    /// <summary>
    /// Appends a reference to <paramref name="artifactId"/> as a new last section, tracking the
    /// source's current version. Returns the created section.
    /// </summary>
    /// <exception cref="InvalidOperationException">The composition or referenced artifact does not exist.</exception>
    ReportSection AddSection(string compositionId, string artifactId);

    /// <summary>Removes section <paramref name="sectionId"/> from the composition. The source artifact is untouched.</summary>
    /// <exception cref="InvalidOperationException">The composition does not exist.</exception>
    void RemoveSection(string compositionId, string sectionId);

    /// <summary>
    /// Reorders the composition's sections to match <paramref name="orderedSectionIds"/> (which must
    /// be a permutation of the current section ids).
    /// </summary>
    /// <exception cref="InvalidOperationException">The composition does not exist, or the ids are not a permutation of its sections.</exception>
    void ReorderSections(string compositionId, IReadOnlyList<string> orderedSectionIds);

    /// <summary>
    /// Pins section <paramref name="sectionId"/> to the fixed artifact version
    /// <paramref name="versionId"/>; the section then renders that version even as the source
    /// advances. The source artifact's version must exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">The composition/section does not exist, or the version does not belong to the section's source artifact.</exception>
    ReportSection PinSectionVersion(string compositionId, string sectionId, string versionId);

    /// <summary>
    /// Renders the composition into a single document in section order (SPEC §3.4.1): each section
    /// under its artifact title as a heading, doc/text/code as content, table as a table, diagram as
    /// its Mermaid source. Unpinned sections reflect the source's current version live; pinned
    /// sections render their fixed version. A section whose source was deleted is flagged and skipped
    /// with a visible placeholder note.
    /// </summary>
    /// <exception cref="InvalidOperationException">The composition does not exist or is not a composition.</exception>
    CompiledReport Render(string compositionId);
}

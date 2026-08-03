using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Reports;

namespace MeticulousResearch.Core.Export;

/// <summary>
/// One artifact contribution to an export: its title, its artifact type (<c>doc | text | code |
/// table | diagram</c>), its current-version content, and any cited sources. This is the export
/// engine's read-only view of an artifact's current version (SPEC §3.4.2) — it never mutates the
/// artifact domain.
/// </summary>
/// <param name="Title">The artifact title (used as a section heading and the cover title).</param>
/// <param name="Type">The artifact type from <see cref="ArtifactTypes"/>.</param>
/// <param name="Content">The current version's content (markdown for docs, CSV for tables, etc.).</param>
/// <param name="Sources">Cited sources for the sources/methodology section, or null/empty.</param>
public sealed record ExportArtifact(
    string Title,
    string Type,
    string Content,
    IReadOnlyList<string>? Sources = null);

/// <summary>
/// The source of a branded export: either a single artifact's current version or a composed report
/// (an ordered set of artifacts) whose section order is preserved (SPEC §3.4.2). Carries the cover
/// metadata (title, subtitle, project) the branded theme places on the cover page.
/// </summary>
public sealed class ExportSource
{
    private ExportSource(
        string title,
        string? subtitle,
        string? project,
        IReadOnlyList<ExportArtifact> artifacts,
        bool isComposedReport)
    {
        Title = title;
        Subtitle = subtitle;
        Project = project;
        Artifacts = artifacts;
        IsComposedReport = isComposedReport;
    }

    /// <summary>The document title (cover title and running-header title).</summary>
    public string Title { get; }

    /// <summary>An optional subtitle placed on the cover page.</summary>
    public string? Subtitle { get; }

    /// <summary>The owning project name placed on the cover page, or null.</summary>
    public string? Project { get; }

    /// <summary>The artifacts contributing content, in export order.</summary>
    public IReadOnlyList<ExportArtifact> Artifacts { get; }

    /// <summary>Whether this source is a composed report (ordered multi-section) vs. a single artifact.</summary>
    public bool IsComposedReport { get; }

    /// <summary>Creates an export source from a single artifact's current version.</summary>
    /// <param name="artifact">The artifact to export.</param>
    /// <param name="subtitle">An optional cover subtitle.</param>
    /// <param name="project">The owning project name for the cover, or null.</param>
    /// <returns>A single-artifact export source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artifact"/> is null.</exception>
    public static ExportSource FromArtifact(ExportArtifact artifact, string? subtitle = null, string? project = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new ExportSource(artifact.Title, subtitle, project, new[] { artifact }, isComposedReport: false);
    }

    /// <summary>Creates an export source from an ordered composed report.</summary>
    /// <param name="title">The report title (cover + running header).</param>
    /// <param name="sections">The ordered section artifacts.</param>
    /// <param name="subtitle">An optional cover subtitle.</param>
    /// <param name="project">The owning project name for the cover, or null.</param>
    /// <returns>A composed-report export source preserving section order.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ExportSource FromReport(
        string title,
        IReadOnlyList<ExportArtifact> sections,
        string? subtitle = null,
        string? project = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(sections);
        return new ExportSource(title, subtitle, project, sections.ToArray(), isComposedReport: true);
    }

    /// <summary>
    /// Creates an export source from a rendered <see cref="CompiledReport"/> (report-composition's
    /// read-only render), preserving its section order (SPEC §3.4.1 → §3.4.2).
    /// </summary>
    /// <param name="title">The report title.</param>
    /// <param name="report">The compiled report whose sections are exported in order.</param>
    /// <param name="subtitle">An optional cover subtitle.</param>
    /// <param name="project">The owning project name for the cover, or null.</param>
    /// <returns>A composed-report export source built from the compiled sections in order.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ExportSource FromCompiledReport(
        string title,
        CompiledReport report,
        string? subtitle = null,
        string? project = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(report);
        var sections = report.Sections
            .Select(s => new ExportArtifact(
                s.Title,
                s.Type ?? ArtifactTypes.Doc,
                s.Body))
            .ToArray();
        return new ExportSource(title, subtitle, project, sections, isComposedReport: true);
    }
}

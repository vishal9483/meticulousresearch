using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Reports;

/// <summary>
/// <see cref="IReportCompositionService"/> layered over <see cref="IArtifactService"/> (SPEC §3.4.1).
/// A composition is a document artifact whose current-version content is a JSON manifest marking it
/// as a composition and holding an ordered list of section references (artifact id + optional pinned
/// version). Mutations append a new manifest version through the artifact-versioning seam, so the
/// composition is listed, versioned, and searchable like any other artifact and sections stay
/// references — never copies. Rendering resolves each section's live-or-pinned content offline.
/// </summary>
public sealed class ReportCompositionService : IReportCompositionService
{
    /// <summary>The manifest marker value identifying a composition artifact.</summary>
    public const string CompositionKind = "report-composition";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IArtifactService _artifacts;

    /// <summary>Creates the composition service over the artifact domain it consumes.</summary>
    /// <param name="artifacts">The artifact service that persists and versions the composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artifacts"/> is null.</exception>
    public ReportCompositionService(IArtifactService artifacts)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
    }

    /// <inheritdoc />
    public Artifact CreateComposition(string projectId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArtifactValidationException("A report composition title is required.");

        var manifest = new CompositionManifest();
        return _artifacts.CreateFromContent(
            projectId, ArtifactTypes.Doc, title, Serialize(manifest), contentFormat: null,
            ArtifactProvenance.User());
    }

    /// <inheritdoc />
    public bool IsComposition(string artifactId)
    {
        var artifact = _artifacts.Get(artifactId);
        if (artifact is null)
            return false;
        return TryReadManifest(artifact, out _);
    }

    /// <inheritdoc />
    public IReadOnlyList<ReportSection> GetSections(string compositionId) =>
        LoadManifest(compositionId).Sections
            .Select(s => new ReportSection(s.SectionId, s.ArtifactId, s.Title, s.PinnedVersionId))
            .ToList();

    /// <inheritdoc />
    public ReportSection AddSection(string compositionId, string artifactId)
    {
        var manifest = LoadManifest(compositionId);
        var source = _artifacts.Get(artifactId)
            ?? throw new InvalidOperationException($"Artifact '{artifactId}' does not exist.");

        var section = new ManifestSection
        {
            SectionId = Guid.NewGuid().ToString("N"),
            ArtifactId = source.Id,
            Title = source.Title,
            PinnedVersionId = null,
        };
        manifest.Sections.Add(section);
        Save(compositionId, manifest);
        return new ReportSection(section.SectionId, section.ArtifactId, section.Title, section.PinnedVersionId);
    }

    /// <inheritdoc />
    public void RemoveSection(string compositionId, string sectionId)
    {
        var manifest = LoadManifest(compositionId);
        manifest.Sections.RemoveAll(s => s.SectionId == sectionId);
        Save(compositionId, manifest);
    }

    /// <inheritdoc />
    public void ReorderSections(string compositionId, IReadOnlyList<string> orderedSectionIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);
        var manifest = LoadManifest(compositionId);

        var byId = manifest.Sections.ToDictionary(s => s.SectionId, StringComparer.Ordinal);
        if (orderedSectionIds.Count != byId.Count || orderedSectionIds.Any(id => !byId.ContainsKey(id)))
            throw new InvalidOperationException(
                "The provided section ids must be a permutation of the composition's current sections.");

        manifest.Sections = orderedSectionIds.Select(id => byId[id]).ToList();
        Save(compositionId, manifest);
    }

    /// <inheritdoc />
    public ReportSection PinSectionVersion(string compositionId, string sectionId, string versionId)
    {
        var manifest = LoadManifest(compositionId);
        var section = manifest.Sections.FirstOrDefault(s => s.SectionId == sectionId)
            ?? throw new InvalidOperationException($"Section '{sectionId}' does not exist in the composition.");

        var history = _artifacts.GetHistory(section.ArtifactId);
        if (history.All(v => v.Id != versionId))
            throw new InvalidOperationException(
                $"Version '{versionId}' does not belong to the section's source artifact.");

        section.PinnedVersionId = versionId;
        Save(compositionId, manifest);
        return new ReportSection(section.SectionId, section.ArtifactId, section.Title, section.PinnedVersionId);
    }

    /// <inheritdoc />
    public CompiledReport Render(string compositionId)
    {
        var manifest = LoadManifest(compositionId);
        var rendered = new List<RenderedSection>(manifest.Sections.Count);

        foreach (var section in manifest.Sections)
        {
            var artifact = _artifacts.Get(section.ArtifactId);
            if (artifact is null)
            {
                rendered.Add(new RenderedSection(
                    section.SectionId, section.Title, Type: null,
                    Body: $"> [Missing section: {section.Title} — source artifact was deleted.]",
                    IsBroken: true));
                continue;
            }

            var content = ResolveContent(artifact, section.PinnedVersionId);
            var body = RenderBody(artifact.Type, content);
            rendered.Add(new RenderedSection(section.SectionId, artifact.Title, artifact.Type, body, IsBroken: false));
        }

        var builder = new StringBuilder();
        foreach (var section in rendered)
        {
            if (builder.Length > 0)
                builder.Append("\n\n");
            builder.Append("## ").Append(section.Title).Append("\n\n").Append(section.Body);
        }

        return new CompiledReport(rendered, builder.ToString());
    }

    private string ResolveContent(Artifact artifact, string? pinnedVersionId)
    {
        var history = _artifacts.GetHistory(artifact.Id);
        var targetId = pinnedVersionId ?? artifact.CurrentVersionId;
        var version = history.FirstOrDefault(v => v.Id == targetId);
        return version?.Content ?? "";
    }

    private static string RenderBody(string type, string content) =>
        type == ArtifactTypes.Table ? RenderTable(content) : content;

    // Renders CSV content as a Markdown table so table rows carry through the compiled document
    // (SPEC §3.4.1). A blank body renders as nothing.
    private static string RenderTable(string csv)
    {
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return "";

        var rows = lines.Select(l => l.Split(',').Select(c => c.Trim()).ToArray()).ToList();
        var columns = rows.Max(r => r.Length);
        var builder = new StringBuilder();

        AppendRow(builder, rows[0], columns);
        builder.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", columns))).Append(" |\n");
        foreach (var row in rows.Skip(1))
            AppendRow(builder, row, columns);

        return builder.ToString().TrimEnd('\n');
    }

    private static void AppendRow(StringBuilder builder, string[] cells, int columns)
    {
        builder.Append("| ");
        for (var i = 0; i < columns; i++)
        {
            builder.Append(i < cells.Length ? cells[i] : "");
            builder.Append(i == columns - 1 ? " |" : " | ");
        }
        builder.Append('\n');
    }

    private CompositionManifest LoadManifest(string compositionId)
    {
        var artifact = _artifacts.Get(compositionId)
            ?? throw new InvalidOperationException($"Composition '{compositionId}' does not exist.");
        if (!TryReadManifest(artifact, out var manifest))
            throw new InvalidOperationException($"Artifact '{compositionId}' is not a report composition.");
        return manifest!;
    }

    private bool TryReadManifest(Artifact artifact, out CompositionManifest? manifest)
    {
        manifest = null;
        var content = CurrentContent(artifact);
        if (string.IsNullOrWhiteSpace(content))
            return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<CompositionManifest>(content, JsonOptions);
            if (parsed is null || parsed.Kind != CompositionKind)
                return false;
            parsed.Sections ??= new List<ManifestSection>();
            manifest = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string CurrentContent(Artifact artifact)
    {
        if (artifact.CurrentVersionId is null)
            return "";
        var version = _artifacts.GetHistory(artifact.Id).FirstOrDefault(v => v.Id == artifact.CurrentVersionId);
        return version?.Content ?? "";
    }

    private void Save(string compositionId, CompositionManifest manifest) =>
        _artifacts.SetContent(compositionId, Serialize(manifest));

    private static string Serialize(CompositionManifest manifest) =>
        JsonSerializer.Serialize(manifest, JsonOptions);

    private sealed class CompositionManifest
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = CompositionKind;

        [JsonPropertyName("sections")]
        public List<ManifestSection> Sections { get; set; } = new();
    }

    private sealed class ManifestSection
    {
        [JsonPropertyName("sectionId")]
        public string SectionId { get; set; } = "";

        [JsonPropertyName("artifactId")]
        public string ArtifactId { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("pinnedVersionId")]
        public string? PinnedVersionId { get; set; }
    }
}

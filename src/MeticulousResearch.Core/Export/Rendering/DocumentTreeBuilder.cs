using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Export.Rendering;

/// <summary>
/// Builds the deterministic document tree (<see cref="RenderedDocument"/>) once from an
/// <see cref="ExportSource"/>, the chosen format/preset, and the brand settings (SPEC §3.4.2). The
/// cover date comes from the injected <see cref="IClock"/>; Mermaid sources are rendered to images
/// for DOCX/PDF (and passed through for Markdown); presets switch how much chrome is present. The
/// resulting tree carries no serialized <see cref="RenderedDocument.Bytes"/> yet — a format writer
/// fills those in.
/// </summary>
internal sealed class DocumentTreeBuilder
{
    private readonly IClock _clock;
    private readonly IDiagramRenderer _diagrams;

    public DocumentTreeBuilder(IClock clock, IDiagramRenderer diagrams)
    {
        _clock = clock;
        _diagrams = diagrams;
    }

    public RenderedDocument Build(ExportSource source, ExportFormat format, ExportPreset preset, BrandSettings brand)
    {
        var accent = brand.ResolvedAccent;
        var styles = new StyleSet(accent);

        if (format == ExportFormat.Xlsx)
        {
            var workbook = ArtifactContentParser.ExtractWorkbook(source.Artifacts[0]);
            return new RenderedDocument
            {
                Format = format,
                Preset = preset,
                Accent = accent,
                Styles = styles,
                Workbook = workbook,
                Bytes = Array.Empty<byte>(),
            };
        }

        var blocks = BuildBlocks(source, format);

        var wantsCover = preset == ExportPreset.ClientReady && format != ExportFormat.Md;
        var wantsToc = preset == ExportPreset.ClientReady && format != ExportFormat.Md;
        var chrome = BuildChrome(source, preset, brand, format);
        var cover = wantsCover ? BuildCover(source, brand) : null;
        var toc = wantsToc ? BuildToc(blocks, hasCover: cover is not null) : null;
        var sources = BuildSources(source, preset, format);

        return new RenderedDocument
        {
            Format = format,
            Preset = preset,
            Accent = accent,
            Styles = styles,
            Cover = cover,
            Toc = toc,
            Chrome = chrome,
            Sources = sources,
            Blocks = blocks,
            Bytes = Array.Empty<byte>(),
        };
    }

    private IReadOnlyList<DocumentBlock> BuildBlocks(ExportSource source, ExportFormat format)
    {
        var blocks = new List<DocumentBlock>();
        foreach (var artifact in source.Artifacts)
        {
            if (source.IsComposedReport)
                blocks.Add(new HeadingBlock(1, artifact.Title, StyleSet.HeadingStyle));

            foreach (var block in ArtifactContentParser.ParseBlocks(artifact))
            {
                if (block is MermaidBlock mermaid && format is ExportFormat.Docx or ExportFormat.Pdf)
                {
                    var image = _diagrams.Render(mermaid.Source);
                    blocks.Add(new ImageBlock("Diagram", image.Bytes, image.Format, "mermaid"));
                }
                else
                {
                    blocks.Add(block);
                }
            }
        }

        return blocks;
    }

    private CoverPage BuildCover(ExportSource source, BrandSettings brand) =>
        new(
            Title: source.Title,
            Subtitle: source.Subtitle,
            Date: _clock.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Project: source.Project,
            LogoPath: brand.LogoPath);

    private static TableOfContents BuildToc(IReadOnlyList<DocumentBlock> blocks, bool hasCover)
    {
        var headings = blocks.OfType<HeadingBlock>().ToList();
        var startPage = 1 + (hasCover ? 1 : 0) + 1; // cover (opt) + TOC page + first content page
        var entries = headings
            .Select((h, i) => new TocEntry(h.Text, h.Level, startPage + i))
            .ToList();
        return new TableOfContents(entries);
    }

    private static RunningChrome? BuildChrome(
        ExportSource source, ExportPreset preset, BrandSettings brand, ExportFormat format)
    {
        if (format == ExportFormat.Md)
            return null;

        return preset switch
        {
            ExportPreset.ClientReady => new RunningChrome(
                Title: source.Title,
                ShowsPageNumber: true,
                Confidentiality: brand.Confidentiality),
            ExportPreset.InternalDraft => new RunningChrome(
                Title: null,
                ShowsPageNumber: true,
                Confidentiality: null),
            _ => null,
        };
    }

    private static SourcesSection? BuildSources(ExportSource source, ExportPreset preset, ExportFormat format)
    {
        if (preset != ExportPreset.ClientReady || format is ExportFormat.Md or ExportFormat.Xlsx)
            return null;

        var sources = source.Artifacts
            .Where(a => a.Sources is not null)
            .SelectMany(a => a.Sources!)
            .ToList();
        return new SourcesSection("Sources & Methodology", sources);
    }
}

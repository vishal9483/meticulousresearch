using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Export;
using MeticulousResearch.Core.Reports;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Export;

/// <summary>
/// Faithful xUnit translation of every scenario in docs/features/branded-export/tests.md
/// (SPEC §3.4.2, §3.7, §9.1(6)). <c>@unit @integration</c> scenarios run in the headless gate over a
/// real <see cref="ExportService"/> with a <see cref="FakeClock"/> for a stable cover date. Two runs
/// on identical input produce byte-identical output and no export path touches the network.
///
/// Background: a project "EV Market 2026" with brand settings (accent navy, logo assets/firm.png,
/// confidentiality "Confidential — Meticulous") and an artifact "Market Report" whose current version
/// contains headings, a table, a list, a code block, and a Mermaid diagram.
/// </summary>
public sealed class BrandedExportTests : IDisposable
{
    private const string Confidentiality = "Confidential — Meticulous";
    private const string LogoPath = "assets/firm.png";
    private const string ClientReady = "Client-ready report";

    private static readonly IReadOnlyList<string> MarketSources =
        new[] { "IEA Global EV Outlook, 2026", "Firm primary interviews, 2026" };

    private const string MarketReportContent = """
# Market Report

## Overview
The EV market is expanding rapidly across all segments.

## Findings
- Demand is rising
- Supply is constrained

## Data
| Segment | Units |
| --- | --- |
| SUV | 1000 |
| Sedan | 800 |

_Figure 1. Segment unit volumes_

## Diagram
```mermaid
graph TD; A-->B;
```

## Method
```python
print("hello")
```
""";

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly ExportService _export;
    private readonly BrandSettings _brand =
        new(Accent: "navy", LogoPath: LogoPath, Confidentiality: Confidentiality);
    private readonly List<string> _tempFiles = new();

    public BrandedExportTests() => _export = new ExportService(_clock);

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    private string TempPath(ExportFormat format)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mr-branded-export-{Guid.NewGuid():N}.{format.Extension()}");
        _tempFiles.Add(path);
        return path;
    }

    private static ExportArtifact MarketReport() =>
        new("Market Report", ArtifactTypes.Doc, MarketReportContent, MarketSources);

    private ExportSource MarketReportSource() =>
        ExportSource.FromArtifact(MarketReport(), subtitle: "EV segment outlook", project: "EV Market 2026");

    // ---------- Formats & source selection ----------

    // Scenario Outline: Exporting the current version to each supported format
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("MD")]
    [InlineData("DOCX")]
    [InlineData("PDF")]
    [InlineData("XLSX")]
    public void Exporting_the_current_version_to_each_supported_format(string formatLabel)
    {
        var format = ExportFormats.Parse(formatLabel);

        // When I export it as "<format>".
        var destination = TempPath(format);
        var result = _export.Export(MarketReportSource(), format, ExportPreset.ClientReady, _brand, destination);

        // Then a "<format>" file is produced.
        Assert.True(result.FileWritten);
        Assert.True(File.Exists(destination));
        Assert.NotEmpty(result.Bytes);
        Assert.Equal(format, result.Document.Format);

        // And its content is derived from the artifact's current version.
        if (format == ExportFormat.Xlsx)
        {
            // The Market Report's current version carries a table -> a workbook is produced.
            Assert.NotNull(result.Document.Workbook);
            Assert.Contains(result.Document.Workbook!.Columns, c => c.Name == "Segment");
        }
        else if (format == ExportFormat.Md)
        {
            Assert.Contains("Market Report", result.Document.Markdown!);
            Assert.Contains("Demand is rising", result.Document.Markdown!);
        }
        else
        {
            Assert.Contains(result.Document.Blocks.OfType<HeadingBlock>(), h => h.Text == "Overview");
        }
    }

    // Scenario: Exporting a composed report uses the section order
    [Fact]
    [Trait("Category", "integration")]
    public void Exporting_a_composed_report_uses_the_section_order()
    {
        // Given a composed report ordering artifacts "Summary", "Sizing", "Landscape".
        var report = ExportSource.FromReport("Full Report", new[]
        {
            new ExportArtifact("Summary", ArtifactTypes.Doc, "Summary body"),
            new ExportArtifact("Sizing", ArtifactTypes.Doc, "Sizing body"),
            new ExportArtifact("Landscape", ArtifactTypes.Doc, "Landscape body"),
        });

        // When I export the composed report as "PDF".
        var document = _export.Preview(report, ExportFormat.Pdf, ExportPreset.ClientReady, _brand);

        // Then the exported document contains those sections in that exact order.
        var sectionTitles = document.Blocks.OfType<HeadingBlock>()
            .Where(h => h.Level == 1)
            .Select(h => h.Text)
            .ToList();
        Assert.Equal(new[] { "Summary", "Sizing", "Landscape" }, sectionTitles);
    }

    // Scenario: A table/dataset artifact is the source for XLSX export
    [Fact]
    public void A_table_dataset_artifact_is_the_source_for_XLSX_export()
    {
        // Given a table artifact "Forecast Model" with typed columns and a formula column.
        var forecast = new ExportArtifact(
            "Forecast Model",
            ArtifactTypes.Table,
            "Year,Revenue,CAGR\n2025,100,=B2/B1-1\n2026,120,=B3/B2-1");
        var source = ExportSource.FromArtifact(forecast);

        // When I export it as "XLSX".
        var document = _export.Preview(source, ExportFormat.Xlsx, ExportPreset.ClientReady, _brand);

        // Then the workbook is produced from that artifact's current version.
        Assert.NotNull(document.Workbook);
        Assert.Equal(new[] { "Year", "Revenue", "CAGR" }, document.Workbook!.Columns.Select(c => c.Name));
        Assert.Equal(2, document.Workbook.Rows.Count);
    }

    // ---------- Branded document theme ----------

    // Scenario: A client-ready export has a cover page
    [Fact]
    [Trait("Category", "integration")]
    public void A_client_ready_export_has_a_cover_page()
    {
        // When I export it as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(
            MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), _brand);

        // Then the document has a cover page with title, subtitle, date, project, and firm logo.
        Assert.NotNull(document.Cover);
        Assert.Equal("Market Report", document.Cover!.Title);
        Assert.Equal("EV segment outlook", document.Cover.Subtitle);
        Assert.Equal("2026-08-03", document.Cover.Date);
        Assert.Equal("EV Market 2026", document.Cover.Project);
        Assert.Equal(LogoPath, document.Cover.LogoPath);
    }

    // Scenario: A client-ready export has an auto table of contents with page numbers
    [Fact]
    [Trait("Category", "integration")]
    public void A_client_ready_export_has_an_auto_table_of_contents_with_page_numbers()
    {
        // When I export it as "DOCX" with the "Client-ready report" preset.
        var document = _export.Preview(
            MarketReportSource(), ExportFormat.Docx, ExportPresets.Parse(ClientReady), _brand);

        // Then a table of contents is generated from the headings.
        Assert.NotNull(document.Toc);
        var headingCount = document.Blocks.OfType<HeadingBlock>().Count();
        Assert.Equal(headingCount, document.Toc!.Entries.Count);
        Assert.Contains(document.Toc.Entries, e => e.Title == "Overview");

        // And each TOC entry has a page number.
        Assert.All(document.Toc.Entries, e => Assert.True(e.PageNumber > 0));
    }

    // Scenario: Running headers and footers carry the title, page number, and confidentiality
    [Fact]
    [Trait("Category", "integration")]
    public void Running_headers_and_footers_carry_title_page_number_and_confidentiality()
    {
        // When I export it as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(
            MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), _brand);

        Assert.NotNull(document.Chrome);

        // Then each page has a running header or footer with the report title.
        Assert.Equal("Market Report", document.Chrome!.Title);

        // And each page shows a page number.
        Assert.True(document.Chrome.ShowsPageNumber);

        // And each page shows the confidentiality notice "Confidential — Meticulous".
        Assert.Equal(Confidentiality, document.Chrome.Confidentiality);
    }

    // Scenario: Headings, tables, lists, captions, and code blocks carry through with consistent styles
    [Fact]
    [Trait("Category", "integration")]
    public void Content_carries_through_with_consistent_styles()
    {
        // When I export it as "DOCX".
        var document = _export.Preview(MarketReportSource(), ExportFormat.Docx, ExportPreset.ClientReady, _brand);

        // Then its headings, tables, lists, captions, and code blocks are present.
        Assert.Contains(document.Blocks, b => b is HeadingBlock);
        Assert.Contains(document.Blocks, b => b is TableBlock);
        Assert.Contains(document.Blocks, b => b is ListBlock);
        Assert.Contains(document.Blocks, b => b is CaptionBlock);
        Assert.Contains(document.Blocks, b => b is CodeBlock);

        // And they use the branded style set (consistent heading, table, and caption styles).
        Assert.Equal(_brand.ResolvedAccent, document.Styles.Accent);
        Assert.All(document.Blocks.OfType<HeadingBlock>(), h => Assert.Equal(StyleSet.HeadingStyle, h.Style));
        Assert.All(document.Blocks.OfType<TableBlock>(), t => Assert.Equal(StyleSet.TableStyle, t.Style));
        Assert.All(document.Blocks.OfType<CaptionBlock>(), c => Assert.Equal(StyleSet.CaptionStyle, c.Style));
    }

    // Scenario: A sources / methodology section is included
    [Fact]
    [Trait("Category", "integration")]
    public void A_sources_methodology_section_is_included()
    {
        // When I export it as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(
            MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), _brand);

        // Then the document contains a sources / methodology section.
        Assert.NotNull(document.Sources);
        Assert.Contains("Methodology", document.Sources!.Title);
        Assert.Equal(MarketSources, document.Sources.Sources);
    }

    // ---------- Mermaid diagrams rendered to images ----------

    // Scenario: A Mermaid diagram is rendered to an image in DOCX/PDF
    [Fact]
    [Trait("Category", "integration")]
    public void A_Mermaid_diagram_is_rendered_to_an_image_in_DOCX_PDF()
    {
        // When I export it as "PDF".
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, ExportPreset.ClientReady, _brand);

        // Then the diagram appears as a rendered image, not as raw Mermaid source.
        Assert.Contains(document.Blocks, b => b is ImageBlock img && img.SourceKind == "mermaid");
        Assert.DoesNotContain(document.Blocks, b => b is MermaidBlock);
    }

    // Scenario: Diagram rendering is offline and deterministic
    [Fact]
    [Trait("Category", "integration")]
    public void Diagram_rendering_is_offline_and_deterministic()
    {
        // When I export it twice with no network available.
        var first = _export.Export(MarketReportSource(), ExportFormat.Pdf, ExportPreset.ClientReady, _brand, TempPath(ExportFormat.Pdf));
        var second = _export.Export(MarketReportSource(), ExportFormat.Pdf, ExportPreset.ClientReady, _brand, TempPath(ExportFormat.Pdf));

        // Then both exports render the diagram identically.
        var firstImage = first.Document.Blocks.OfType<ImageBlock>().Single(i => i.SourceKind == "mermaid");
        var secondImage = second.Document.Blocks.OfType<ImageBlock>().Single(i => i.SourceKind == "mermaid");
        Assert.Equal(firstImage.Image, secondImage.Image);

        // And no network request is made.
        Assert.Equal(0, first.NetworkRequests);
        Assert.Equal(0, second.NetworkRequests);
    }

    // ---------- XLSX fidelity ----------

    // Scenario: XLSX preserves typed columns
    [Fact]
    [Trait("Category", "integration")]
    public void XLSX_preserves_typed_columns()
    {
        // Given a table artifact with a text column, a number column, and a date column.
        var table = new ExportArtifact(
            "Typed",
            ArtifactTypes.Table,
            "Name,Count,When\nAlpha,10,2026-01-01\nBeta,20,2026-02-01");
        var document = _export.Preview(ExportSource.FromArtifact(table), ExportFormat.Xlsx, ExportPreset.Plain, _brand);

        // Then each column cell carries its declared type (text, number, date).
        var wb = document.Workbook!;
        Assert.Equal(WorkbookCellType.Text, wb.Columns[0].Type);
        Assert.Equal(WorkbookCellType.Number, wb.Columns[1].Type);
        Assert.Equal(WorkbookCellType.Date, wb.Columns[2].Type);
        Assert.All(wb.Rows, r => Assert.Equal(WorkbookCellType.Text, r[0].Type));
        Assert.All(wb.Rows, r => Assert.Equal(WorkbookCellType.Number, r[1].Type));
        Assert.All(wb.Rows, r => Assert.Equal(WorkbookCellType.Date, r[2].Type));
    }

    // Scenario: XLSX preserves formulas where present
    [Fact]
    [Trait("Category", "integration")]
    public void XLSX_preserves_formulas_where_present()
    {
        // Given a forecast table with a CAGR column defined by a formula.
        var forecast = new ExportArtifact(
            "Forecast",
            ArtifactTypes.Table,
            "Year,Revenue,CAGR\n2025,100,=B2/B1-1\n2026,120,=B3/B2-1");
        var document = _export.Preview(ExportSource.FromArtifact(forecast), ExportFormat.Xlsx, ExportPreset.Plain, _brand);

        // Then the formula cells contain the formula, not just a static value.
        var cagrCells = document.Workbook!.Rows.Select(r => r[2]).ToList();
        Assert.All(cagrCells, c => Assert.Equal(WorkbookCellType.Formula, c.Type));
        Assert.All(cagrCells, c => Assert.StartsWith("=", c.Raw));
    }

    // Scenario: Non-tabular content cannot be exported to XLSX
    [Fact]
    [Trait("Category", "integration")]
    public void Non_tabular_content_cannot_be_exported_to_XLSX()
    {
        // Given a prose document artifact with no table.
        var prose = new ExportArtifact("Prose", ArtifactTypes.Doc, "# Prose\n\nJust paragraphs, no table here.");
        var destination = TempPath(ExportFormat.Xlsx);

        // When I try to export it as "XLSX".
        var ex = Assert.Throws<XlsxRequiresTableException>(() =>
            _export.Export(ExportSource.FromArtifact(prose), ExportFormat.Xlsx, ExportPreset.Plain, _brand, destination));

        // Then I am told XLSX requires a table/dataset artifact.
        Assert.Contains("table", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And no file is produced.
        Assert.False(File.Exists(destination));
    }

    // ---------- Determinism & offline ----------

    // Scenario Outline: The same input produces the same output on repeat export
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("MD")]
    [InlineData("DOCX")]
    [InlineData("PDF")]
    [InlineData("XLSX")]
    public void The_same_input_produces_the_same_output_on_repeat_export(string formatLabel)
    {
        var format = ExportFormats.Parse(formatLabel);

        // When I export it as "<format>" twice with a fixed clock.
        var first = _export.Export(MarketReportSource(), format, ExportPreset.ClientReady, _brand, TempPath(format));
        var second = _export.Export(MarketReportSource(), format, ExportPreset.ClientReady, _brand, TempPath(format));

        // Then the two outputs are identical.
        Assert.Equal(first.Bytes, second.Bytes);
        Assert.Equal(File.ReadAllBytes(first.DestinationPath), File.ReadAllBytes(second.DestinationPath));
    }

    // Scenario: Export makes no network calls
    [Fact]
    [Trait("Category", "integration")]
    public void Export_makes_no_network_calls()
    {
        // When I export it as "PDF".
        var result = _export.Export(MarketReportSource(), ExportFormat.Pdf, ExportPreset.ClientReady, _brand, TempPath(ExportFormat.Pdf));

        // Then no network request is made during export.
        Assert.Equal(0, result.NetworkRequests);
    }

    // Scenario: A fixed clock produces a stable cover date
    [Fact]
    public void A_fixed_clock_produces_a_stable_cover_date()
    {
        // Given the clock is set to "2026-08-03" (see _clock).
        // When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(
            MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), _brand);

        // Then the cover date reads "2026-08-03".
        Assert.Equal("2026-08-03", document.Cover!.Date);
    }

    // ---------- Preview before save ----------

    // Scenario: A preview is produced before the file is written to disk
    [Fact]
    public void A_preview_is_produced_before_the_file_is_written_to_disk()
    {
        var destination = TempPath(ExportFormat.Pdf);

        // When I request a "PDF" export preview.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, ExportPreset.ClientReady, _brand);

        // Then a preview of the branded document is produced.
        Assert.NotNull(document);
        Assert.NotEmpty(document.Bytes);

        // And nothing is written to the export destination yet.
        Assert.False(File.Exists(destination));
    }

    // ---------- Export presets ----------

    // Scenario Outline: Presets control the amount of chrome
    [Theory]
    [Trait("Category", "integration")]
    [InlineData("Client-ready report", true, true, "present")]
    [InlineData("Internal draft", false, false, "minimal")]
    [InlineData("Plain", false, false, "absent")]
    public void Presets_control_the_amount_of_chrome(string presetLabel, bool cover, bool toc, string chrome)
    {
        var preset = ExportPresets.Parse(presetLabel);

        // When I export it as "PDF" with the "<preset>" preset.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, preset, _brand);

        // Then the cover page is <cover>.
        Assert.Equal(cover, document.Cover is not null);

        // And the table of contents is <toc>.
        Assert.Equal(toc, document.Toc is not null);

        // And the running header/footer is <chrome>.
        switch (chrome)
        {
            case "present":
                Assert.NotNull(document.Chrome);
                Assert.True(document.Chrome!.IsFull);
                break;
            case "minimal":
                Assert.NotNull(document.Chrome);
                Assert.False(document.Chrome!.IsFull);
                break;
            case "absent":
                Assert.Null(document.Chrome);
                break;
            default:
                throw new InvalidOperationException($"Unexpected chrome '{chrome}'.");
        }
    }

    // Scenario: The Plain preset emits content only
    [Fact]
    public void The_Plain_preset_emits_content_only()
    {
        // When I export it as "MD" with the "Plain" preset.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Md, ExportPreset.Plain, _brand);

        // Then the output contains the artifact content.
        Assert.Contains("Market Report", document.Markdown!);
        Assert.Contains("Demand is rising", document.Markdown!);

        // And it contains no cover page, TOC, header, or footer chrome.
        Assert.Null(document.Cover);
        Assert.Null(document.Toc);
        Assert.Null(document.Chrome);
        Assert.DoesNotContain("<!--", document.Markdown!);
    }

    // ---------- Configurable brand accent & logo ----------

    // Scenario: The configured accent color is applied to the branded theme
    [Fact]
    [Trait("Category", "integration")]
    public void The_configured_accent_color_is_applied_to_the_branded_theme()
    {
        // Given the brand accent is set to "navy" in Settings.
        var brand = new BrandSettings(Accent: "navy", LogoPath: LogoPath, Confidentiality: Confidentiality);

        // When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), brand);

        // Then the branded accent used in the document is "navy".
        Assert.Equal("navy", document.Accent);
    }

    // Scenario: The configured logo is placed on the cover and/or header
    [Fact]
    [Trait("Category", "integration")]
    public void The_configured_logo_is_placed_on_the_cover()
    {
        // Given a firm logo configured in Settings.
        var brand = new BrandSettings(Accent: "navy", LogoPath: LogoPath, Confidentiality: Confidentiality);

        // When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), brand);

        // Then the firm logo appears on the cover page.
        Assert.Equal(LogoPath, document.Cover!.LogoPath);
    }

    // Scenario: A default professional navy palette is used when no accent is configured
    [Fact]
    [Trait("Category", "integration")]
    public void A_default_navy_palette_is_used_when_no_accent_is_configured()
    {
        // Given no brand accent is configured.
        var brand = new BrandSettings(Accent: null, LogoPath: LogoPath, Confidentiality: Confidentiality);

        // When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset.
        var document = _export.Preview(MarketReportSource(), ExportFormat.Pdf, ExportPresets.Parse(ClientReady), brand);

        // Then the default navy corporate accent is applied.
        Assert.Equal(BrandSettings.DefaultNavyAccent, document.Accent);
        Assert.Equal("navy", document.Accent);
    }

    // Scenario: The client-ready PDF looks like a professional firm deliverable
    [Fact(Skip = "Manual: publication-quality branding checklist reviewed in the PR (SPEC §3.4.2, §9.1(6)).")]
    [Trait("Category", "manual")]
    public void The_client_ready_PDF_looks_like_a_professional_firm_deliverable()
    {
        // Manual branding checklist (checked off in the PR):
        //  [ ] Cover page: title, subtitle, date, project, firm logo read as publication-quality.
        //  [ ] Auto TOC entries align with headings and show correct page numbers.
        //  [ ] Running header/footer carry the title, page number, and confidentiality on every page.
        //  [ ] Typography and table styles are consistent and professional.
        //  [ ] Mermaid diagrams render crisply as images; no raw Mermaid source appears.
        //  [ ] Sources / methodology section is present and formatted.
    }
}

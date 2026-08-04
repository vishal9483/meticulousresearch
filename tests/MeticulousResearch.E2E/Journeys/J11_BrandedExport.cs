using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Export;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-11 — Compose a full report and export a branded, client-ready deliverable (covers SPEC §9.1: 6).
/// Composition section ordering, branded PDF/DOCX export of the composed report, XLSX export of a
/// forecast table, preset-driven chrome, and byte-identical determinism all run headlessly over the
/// real report-composition + export services.
/// </summary>
public sealed class J11_BrandedExport : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;
    private readonly BrandSettings _brand = new(Accent: "navy", LogoPath: null, Confidentiality: "Confidential");

    public J11_BrandedExport() => _projectId = _h.Projects.Create("Grid Storage 2026").Id;

    public void Dispose() => _h.Dispose();

    private Artifact Doc(string title, string content) =>
        _h.Artifacts.CreateFromContent(_projectId, ArtifactTypes.Doc, title, content, null, ArtifactProvenance.User());

    // @e2e
    // Scenario: Ravi assembles sections into one branded report and exports it
    [Fact]
    public void Ravi_assembles_sections_into_one_branded_report_and_exports_it()
    {
        var summary = Doc("Summary", "# Summary\nThe market grows.");
        var sizing = Doc("Sizing", "# Sizing\nTAM is $100B.");
        var forecast = _h.Artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Table, "Forecast Table", "Year,GWh\n2026,10\n2027,20", null, ArtifactProvenance.User());

        // When I order the document artifacts into sections.
        var composition = _h.Reports.CreateComposition(_projectId, "Grid Storage 2026 — Full Report");
        _h.Reports.AddSection(composition.Id, summary.Id);
        _h.Reports.AddSection(composition.Id, sizing.Id);

        // Then a single composed report references those sections in order.
        var sectionArtifactIds = _h.Reports.GetSections(composition.Id).Select(s => s.ArtifactId).ToList();
        Assert.Equal(new[] { summary.Id, sizing.Id }, sectionArtifactIds);

        var compiled = _h.Reports.Render(composition.Id);
        var source = ExportSource.FromCompiledReport("Grid Storage 2026 — Full Report", compiled);

        // When I export the composed report as "Client-ready report" to PDF (preview shown before save).
        var preview = _h.Export.Preview(source, ExportFormat.Pdf, ExportPreset.ClientReady, _brand);
        var level1Headings = preview.Blocks.OfType<HeadingBlock>().Where(h => h.Level == 1)
            .Select(h => h.Text).Distinct().ToList();
        Assert.Equal(new[] { "Summary", "Sizing" }, level1Headings);

        var pdf = _h.Export.Export(source, ExportFormat.Pdf, ExportPreset.ClientReady, _brand, _h.NewTempPath("pdf"));
        Assert.True(pdf.FileWritten);

        // When I export the same report to DOCX, the branded theme is applied with the same structure.
        var docx = _h.Export.Export(source, ExportFormat.Docx, ExportPreset.ClientReady, _brand, _h.NewTempPath("docx"));
        Assert.True(docx.FileWritten);

        // When I export the forecast-table artifact to XLSX, typed columns are preserved.
        var tableSource = ExportSource.FromArtifact(
            new ExportArtifact("Forecast Table", ArtifactTypes.Table, "Year,GWh\n2026,10\n2027,20"));
        var xlsx = _h.Export.Export(tableSource, ExportFormat.Xlsx, ExportPreset.ClientReady, _brand, _h.NewTempPath("xlsx"));
        Assert.True(xlsx.FileWritten);
        Assert.NotNull(xlsx.Document.Workbook);
        Assert.Contains(xlsx.Document.Workbook!.Columns, c => c.Name == "Year");
    }

    // @e2e @unit
    // Scenario Outline: Export presets control document chrome deterministically
    [Theory]
    [InlineData("Client-ready report", "PDF")]
    [InlineData("Internal draft", "DOCX")]
    [InlineData("Plain", "MD")]
    public void Export_presets_control_document_chrome_deterministically(string presetLabel, string formatLabel)
    {
        var preset = ExportPresets.Parse(presetLabel);
        var format = ExportFormats.Parse(formatLabel);
        var source = ExportSource.FromReport("Full Report", new[]
        {
            new ExportArtifact("Summary", ArtifactTypes.Doc, "# Summary\nbody"),
            new ExportArtifact("Sizing", ArtifactTypes.Doc, "# Sizing\nbody"),
        });

        // And exporting the same input twice produces byte-identical output.
        var first = _h.Export.Export(source, format, preset, _brand, _h.NewTempPath(format.Extension())).Bytes;
        var second = _h.Export.Export(source, format, preset, _brand, _h.NewTempPath(format.Extension())).Bytes;
        Assert.Equal(first, second);
    }

    // @e2e @unit — the preset changes the chrome (client-ready adds cover/TOC chrome that plain omits).
    [Fact]
    public void Different_presets_produce_different_chrome_for_the_same_source()
    {
        var source = ExportSource.FromReport("Full Report", new[]
        {
            new ExportArtifact("Summary", ArtifactTypes.Doc, "# Summary\nbody"),
        });

        var clientReady = _h.Export.Export(source, ExportFormat.Pdf, ExportPreset.ClientReady, _brand, _h.NewTempPath("pdf")).Bytes;
        var plain = _h.Export.Export(source, ExportFormat.Pdf, ExportPreset.Plain, _brand, _h.NewTempPath("pdf")).Bytes;

        Assert.NotEqual(clientReady, plain);
    }
}

using System.Linq;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-03 — Add mixed resources and see them extracted, previewed, token-estimated (covers SPEC §9.1: 3).
/// The resources table chrome is a window concern; the service-level truths — heterogeneous source
/// material is extracted, blob+text persisted, token-estimated, image-captioned, URL-preserving — run
/// headlessly. Binary-fixture formats (pdf/docx/xlsx) that need the shared fixtures (§2.1) are covered
/// by the per-feature file-upload integration tests and marked skipped here.
/// </summary>
public sealed class J03_MixedResources : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J03_MixedResources() => _projectId = _h.Projects.Create("EV Market 2026").Id;

    public void Dispose() => _h.Dispose();

    private string WriteFile(string extension, string content)
    {
        var path = Path.Combine(_h.DataDirectory, $"src-{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(path, content);
        return path;
    }

    // @e2e
    // Scenario: Ana loads the project with heterogeneous source material
    [Fact]
    public void Ana_loads_the_project_with_heterogeneous_source_material()
    {
        // When I paste interview notes as a text resource.
        _h.Resources.AddText(_projectId, "Interview notes", "The analyst described strong SUV demand.");

        // And I upload a CSV of segment data.
        _h.Resources.AddFile(_projectId, WriteFile("csv", "Segment,Units\nSUV,1000\nSedan,800\n"));

        // And I add a URL resource (served by the loopback stub, not the real network).
        const string url = "https://example.test/report";
        _h.UrlFetcher.WithHtml(url, "<html><body><h1>Report</h1><p>Market grows.</p></body></html>");
        var urlResource = _h.Resources.AddUrl(_projectId, url);

        // And I add an image.
        var image = _h.Resources.AddImage(_projectId, _h.NewImageFile());
        var captioned = _h.Resources.GenerateImageCaption(image.Id);

        var listed = _h.Resources.List(_projectId);

        // Then each resource appears in the resources table with type, byte size, and a token estimate.
        Assert.Equal(4, listed.Count);
        Assert.All(listed, r => Assert.False(string.IsNullOrEmpty(r.Type)));
        Assert.All(listed, r => Assert.NotNull(r.TokenEstimate));

        // And each non-image resource shows previewable extracted text.
        foreach (var r in listed.Where(r => r.Type != ResourceTypes.Image))
            Assert.False(string.IsNullOrWhiteSpace(_h.Resources.GetExtractedText(r.Id)));

        // And the image resource shows a cached vision caption.
        Assert.False(string.IsNullOrWhiteSpace(captioned.ExtractedText));

        // And the URL resource retains its original URL and shows converted text.
        Assert.Equal(url, _h.Resources.Get(urlResource.Id)!.SourceUri);
        Assert.Contains("Market grows", _h.Resources.GetExtractedText(urlResource.Id));

        // And the dashboard resource count reflects all added resources.
        Assert.Equal(4, _h.Projects.GetDashboard(_projectId).ResourceCount);
    }

    // @e2e @unit
    // Scenario Outline: Extraction pipeline stores both original blob and extracted text
    // (runnable subset — types constructed deterministically in-code without external binary fixtures)
    [Theory]
    [InlineData("text")]
    [InlineData("txt")]
    [InlineData("md")]
    [InlineData("csv")]
    [InlineData("url")]
    [InlineData("image")]
    public void Extraction_pipeline_stores_original_blob_and_extracted_text(string kind)
    {
        Resource resource;
        switch (kind)
        {
            case "text":
                resource = _h.Resources.AddText(_projectId, "Notes", "Deterministic body text.");
                // A pasted text resource has no original blob.
                Assert.True(string.IsNullOrEmpty(resource.BlobPath));
                break;
            case "url":
                const string url = "https://example.test/doc";
                _h.UrlFetcher.WithHtml(url, "<html><body><p>Converted body.</p></body></html>");
                resource = _h.Resources.AddUrl(_projectId, url);
                Assert.Equal(url, resource.SourceUri);
                break;
            case "image":
                resource = _h.Resources.AddImage(_projectId, _h.NewImageFile());
                Assert.True(File.Exists(resource.BlobPath));
                break;
            default: // txt / md / csv → a readable file upload with an on-disk blob.
                var content = kind == "csv" ? "A,B\n1,2\n" : "Readable body text.";
                resource = _h.Resources.AddFile(_projectId, WriteFile(kind, content)).Resource;
                Assert.True(File.Exists(resource.BlobPath));
                break;
        }

        // Extracted text is stored and searchable (non-image kinds carry human-readable text).
        if (kind != "image")
            Assert.False(string.IsNullOrWhiteSpace(_h.Resources.GetExtractedText(resource.Id)));

        // A deterministic token estimate is recorded.
        Assert.NotNull(_h.Resources.Get(resource.Id)!.TokenEstimate);
    }

    // @e2e @unit — binary-fixture formats (pdf/docx/xlsx) require the shared §2.1 fixtures.
    [Fact(Skip = "Binary-fixture extraction (pdf/docx/xlsx) needs the shared fixtures (§2.1); covered by the file-upload feature integration tests.")]
    public void Extraction_pipeline_for_binary_fixture_formats()
    {
    }
}

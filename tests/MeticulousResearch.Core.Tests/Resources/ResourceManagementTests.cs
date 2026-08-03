using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of the @unit (+ @integration) management scenarios in
/// docs/features/resource-management/tests.md: rename, enable/disable + generation scope,
/// re-extract, preview, and remove. These touch a temp SQLite store + file layout (allowed for
/// @unit per TESTING-STRATEGY §4) so they run in the headless gate.
/// Background: "a project 'Semiconductors 2026' with several resources is open".
/// </summary>
public sealed class ResourceManagementTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;
    private readonly string _fixtureDir;

    public ResourceManagementTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-resource-mgmt-tests", Guid.NewGuid().ToString("N"));
        _fixtureDir = Path.Combine(_dataDir, "fixtures");
        Directory.CreateDirectory(_fixtureDir);
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
        _service = new ResourceService(_store, new HeuristicTokenEstimator());
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // Scenario: Renaming a resource updates its title and timestamp
    [Fact]
    public void Renaming_a_resource_updates_its_title_and_timestamp()
    {
        // Given a resource titled "Foundry note"
        var resource = _service.AddText(_projectId, "Foundry note", "capacity grew 12%.");
        var originalUpdatedAt = resource.UpdatedAt;

        // (advance the clock so a new timestamp is observably newer)
        _clock.Advance(TimeSpan.FromMinutes(5));

        // When I rename it to "Foundry capacity note"
        _service.Rename(resource.Id, "Foundry capacity note");

        // Then the resource is titled "Foundry capacity note"
        var stored = _service.Get(resource.Id)!;
        Assert.Equal("Foundry capacity note", stored.Title);

        // And its updated_at is newer than before
        Assert.True(string.CompareOrdinal(stored.UpdatedAt, originalUpdatedAt) > 0);
    }

    // Scenario: A resource title cannot be blank
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_resource_title_cannot_be_blank(string blank)
    {
        // Given a resource titled "Foundry note"
        var resource = _service.AddText(_projectId, "Foundry note", "capacity grew 12%.");

        // When I try to rename it to an empty title / Then I see a validation error
        Assert.Throws<ArgumentException>(() => _service.Rename(resource.Id, blank));

        // And the title is unchanged
        Assert.Equal("Foundry note", _service.Get(resource.Id)!.Title);
    }

    // Scenario: Disabling a resource removes it from generation scope
    [Fact]
    public void Disabling_a_resource_removes_it_from_generation_scope()
    {
        // Given an enabled resource "Shipments 2025"
        var resource = _service.AddText(_projectId, "Shipments 2025", "shipments rose.");
        Assert.True(_service.Get(resource.Id)!.Enabled);

        // When I disable it
        _service.SetEnabled(resource.Id, false);

        // Then it is marked disabled
        Assert.False(_service.Get(resource.Id)!.Enabled);

        // And it is excluded from the assembled generation context
        Assert.DoesNotContain(_service.ListEnabled(_projectId), r => r.Id == resource.Id);
    }

    // Scenario: Enabling a resource returns it to scope
    [Fact]
    public void Enabling_a_resource_returns_it_to_scope()
    {
        // Given a disabled resource "Shipments 2025"
        var resource = _service.AddText(_projectId, "Shipments 2025", "shipments rose.");
        _service.SetEnabled(resource.Id, false);
        Assert.False(_service.Get(resource.Id)!.Enabled);

        // When I enable it
        _service.SetEnabled(resource.Id, true);

        // Then it is marked enabled
        Assert.True(_service.Get(resource.Id)!.Enabled);

        // And it is included in the assembled generation context
        Assert.Contains(_service.ListEnabled(_projectId), r => r.Id == resource.Id);
    }

    // Scenario: Re-extracting a file resource regenerates its extracted text
    [Fact]
    public void Re_extracting_a_file_resource_regenerates_its_extracted_text()
    {
        // Given a file resource whose extracted text was previously produced
        const string content = "Wafer starts rose sharply across all nodes.";
        var path = FileFixtures.WritePlainText(_fixtureDir, "wafer", "txt", content);
        var added = _service.AddFile(_projectId, path);
        var resourceId = added.Resource.Id;
        Assert.Equal(content, _service.GetExtractedText(resourceId));

        // (corrupt the on-disk extracted text so a genuine re-run against the stored original is proven)
        File.WriteAllText(added.Resource.ExtractedPath!, "STALE — replaced", new UTF8Encoding(false));
        Assert.Equal("STALE — replaced", _service.GetExtractedText(resourceId));

        // When I re-extract it
        var result = _service.ReExtract(resourceId);

        // Then extraction runs again against the stored original / And the extracted text is refreshed
        Assert.Equal(content, _service.GetExtractedText(resourceId));

        // And its token_estimate is recomputed
        var expectedEstimate = new HeuristicTokenEstimator().Estimate(content);
        Assert.Equal(expectedEstimate, _service.Get(resourceId)!.TokenEstimate);
        Assert.Equal(expectedEstimate, result.Resource.TokenEstimate);
    }

    // Scenario: Re-extracting recovers a previously failed extraction
    [Fact]
    public void Re_extracting_recovers_a_previously_failed_extraction()
    {
        // Given a file resource with extraction status "failed" (a corrupt PDF that cannot be parsed)
        var corruptPath = FileFixtures.WriteCorruptPdf(_fixtureDir, "broken");
        var added = _service.AddFile(_projectId, corruptPath);
        Assert.Equal(ExtractionStatus.Failed, added.Status);
        Assert.Equal("", _service.GetExtractedText(added.Resource.Id));

        // When I re-extract it with a working extractor (a pipeline whose pdf extractor now succeeds)
        var workingPipeline = new FileExtractionPipeline(new ITextExtractor[]
        {
            new FakePdfExtractor("Recovered text from the once-broken file."),
        });
        var recoveringService = new ResourceService(_store, new HeuristicTokenEstimator(), workingPipeline);
        var result = recoveringService.ReExtract(added.Resource.Id);

        // Then its status becomes "extracted"
        Assert.Equal(ExtractionStatus.Success, result.Status);

        // And its extracted text is populated
        Assert.Equal("Recovered text from the once-broken file.", recoveringService.GetExtractedText(added.Resource.Id));
    }

    // Scenario: Re-extract is unavailable for a text-paste resource
    [Fact]
    public void Re_extract_is_unavailable_for_a_text_paste_resource()
    {
        // Given a text-paste resource
        var resource = _service.AddText(_projectId, "Inline", "inline note.");

        // Then no "re-extract" action is offered (the operation is refused for pasted text)
        Assert.Throws<NotSupportedException>(() => _service.ReExtract(resource.Id));
    }

    // Scenario: Previewing shows the current extracted text
    [Fact]
    public void Previewing_shows_the_current_extracted_text()
    {
        // Given a resource with extracted text "Wafer starts rose sharply."
        var resource = _service.AddText(_projectId, "Wafer", "Wafer starts rose sharply.");

        // When I preview it / Then I see "Wafer starts rose sharply."
        Assert.Equal("Wafer starts rose sharply.", _service.GetExtractedText(resource.Id));
    }

    // Scenario: Removing a resource deletes its row and files
    [Fact]
    [Trait("Category", "integration")]
    public void Removing_a_resource_deletes_its_row_and_files()
    {
        // Given a resource with an original blob and extracted text on disk
        var path = FileFixtures.WritePlainText(_fixtureDir, "note", "txt", "some content on disk.");
        var added = _service.AddFile(_projectId, path);
        var resourceId = added.Resource.Id;
        var resourceDir = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", resourceId);
        Assert.True(File.Exists(added.Resource.BlobPath!));
        Assert.True(File.Exists(added.Resource.ExtractedPath!));
        Assert.True(Directory.Exists(resourceDir));

        // When I remove it and confirm
        _service.Remove(resourceId);

        // Then the resource no longer exists
        Assert.Null(_service.Get(resourceId));
        Assert.DoesNotContain(_service.List(_projectId), r => r.Id == resourceId);

        // And its "projects/{projectId}/resources/{resourceId}" directory is deleted
        Assert.False(Directory.Exists(resourceDir));
    }

    /// <summary>A stand-in extractor that handles pdf files and always yields fixed text.</summary>
    private sealed class FakePdfExtractor : ITextExtractor
    {
        private readonly string _text;
        public FakePdfExtractor(string text) => _text = text;
        public bool CanHandle(string extension) => extension == "pdf";
        public ExtractedContent Extract(string filePath) => new(_text);
    }
}

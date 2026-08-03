using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/text-paste-resource/tests.md.
/// These are @unit and run in the headless gate; they touch a temp SQLite database + file layout so
/// they carry no excluded Category trait (TESTING-STRATEGY §4 — @unit may touch a temp store).
/// Background: "a project 'Semiconductors 2026' is open" — created per test.
/// </summary>
public sealed class ResourceServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;

    public ResourceServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-text-paste-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
        _service = new ResourceService(_store, new HeuristicTokenEstimator());
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
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

    // Scenario: Pasting text creates an enabled resource
    [Fact]
    public void Pasting_text_creates_an_enabled_resource()
    {
        // When I paste "..." with title "Foundry note"
        var resource = _service.AddText(_projectId, "Foundry note", "Global foundry capacity grew 12% in 2025.");

        // Then a resource "Foundry note" of type "text" exists in the project
        var stored = _service.Get(resource.Id);
        Assert.NotNull(stored);
        Assert.Equal("Foundry note", stored!.Title);
        Assert.Equal("text", stored.Type);
        Assert.Contains(_service.List(_projectId), r => r.Id == resource.Id);

        // And it is enabled
        Assert.True(stored.Enabled);

        // And its added timestamp is set
        Assert.False(string.IsNullOrWhiteSpace(stored.CreatedAt));
    }

    // Scenario: A pasted resource stores its text as extracted text
    [Fact]
    public void A_pasted_resource_stores_its_text_as_extracted_text()
    {
        // Given I paste "Wafer starts rose sharply." with title "Wafer note"
        var resource = _service.AddText(_projectId, "Wafer note", "Wafer starts rose sharply.");

        // When the resource is saved / Then its extracted text is "Wafer starts rose sharply."
        Assert.Equal("Wafer starts rose sharply.", _service.GetExtractedText(resource.Id));

        // And the extracted text is written under
        // "projects/{projectId}/resources/{resourceId}/extracted.txt"
        var expectedPath = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", resource.Id, "extracted.txt");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("Wafer starts rose sharply.", File.ReadAllText(expectedPath));
    }

    // Scenario: A pasted text resource has no original blob
    [Fact]
    public void A_pasted_text_resource_has_no_original_blob()
    {
        // Given I paste "Inline snippet." with title "Snippet" / When the resource is saved
        var resource = _service.AddText(_projectId, "Snippet", "Inline snippet.");
        var stored = _service.Get(resource.Id)!;

        // Then its blob_path is empty
        Assert.True(string.IsNullOrEmpty(stored.BlobPath));

        // And its source_uri is empty
        Assert.True(string.IsNullOrEmpty(stored.SourceUri));
    }

    // Scenario: Title defaults from the first line when omitted
    [Fact]
    public void Title_defaults_from_the_first_line_when_omitted()
    {
        // Given I paste "Market summary\nrest of the text" with no title / When the resource is saved
        var resource = _service.AddText(_projectId, null, "Market summary\nrest of the text");

        // Then its title is "Market summary"
        Assert.Equal("Market summary", _service.Get(resource.Id)!.Title);
    }

    // Scenario: Pasting empty text is rejected
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public void Pasting_empty_text_is_rejected(string emptyOrWhitespace)
    {
        // When I try to paste text that is empty or whitespace only
        // Then I see an inline validation error (surfaced here as a rejected operation)
        Assert.Throws<ArgumentException>(() => _service.AddText(_projectId, "Title", emptyOrWhitespace));

        // And no resource is created
        Assert.Empty(_service.List(_projectId));
    }

    // Scenario: A text resource records byte size and a token estimate
    [Fact]
    public void A_text_resource_records_byte_size_and_a_token_estimate()
    {
        const string text = "Global foundry capacity grew 12% in 2025.";

        // Given I paste "..." with title "Foundry note" / When the resource is saved
        var resource = _service.AddText(_projectId, "Foundry note", text);
        var stored = _service.Get(resource.Id)!;

        // Then its byte_size equals the UTF-8 byte length of the text
        Assert.Equal(Encoding.UTF8.GetByteCount(text), stored.ByteSize);

        // And its token_estimate is a positive number
        Assert.NotNull(stored.TokenEstimate);
        Assert.True(stored.TokenEstimate > 0);
    }

    // Scenario: A saved resource re-reads all fields after reopening the project
    [Fact]
    public void A_saved_resource_re_reads_all_fields_after_reopening_the_project()
    {
        // Given I paste "Persisted text." with title "Persisted"
        var resource = _service.AddText(_projectId, "Persisted", "Persisted text.");

        // When I close and reopen the project (a fresh store + service over the same data dir)
        _store.ClearConnectionPool();
        _store.Dispose();
        using var reopened = new DataStore(_clock, _dataDir);
        reopened.Initialize();
        var reopenedService = new ResourceService(reopened, new HeuristicTokenEstimator());

        // Then the resource "Persisted" is present with type "text", enabled true, extracted intact
        var stored = reopenedService.Get(resource.Id);
        Assert.NotNull(stored);
        Assert.Equal("Persisted", stored!.Title);
        Assert.Equal("text", stored.Type);
        Assert.True(stored.Enabled);
        Assert.Equal("Persisted text.", reopenedService.GetExtractedText(resource.Id));
    }

    // Scenario: Previewing a text resource shows its extracted text
    [Fact]
    public void Previewing_a_text_resource_shows_its_extracted_text()
    {
        // Given a text resource "Foundry note" with text "..."
        var resource = _service.AddText(
            _projectId, "Foundry note", "Global foundry capacity grew 12% in 2025.");

        // When I preview the resource / Then the preview shows "..."
        Assert.Equal("Global foundry capacity grew 12% in 2025.", _service.GetExtractedText(resource.Id));
    }
}

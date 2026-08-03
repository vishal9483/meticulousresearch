using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Url;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of the @unit / @integration scenarios in
/// docs/features/url-resource/tests.md. URL fetching is served by a <see cref="FakeUrlFetcher"/> with
/// scripted responses so every scenario is deterministic and offline (no real network — the phase's
/// non-negotiable). These are @unit/@integration and run in the headless gate: they touch a temp
/// SQLite store + file layout, so they carry no excluded Category trait (TESTING-STRATEGY §4).
/// Background: "a project 'Semiconductors 2026' is open" — created per test.
/// </summary>
public sealed class UrlResourceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly FakeUrlFetcher _fetcher = new();
    private readonly ResourceService _service;
    private readonly string _projectId;

    public UrlResourceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-url-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
        _service = new ResourceService(_store, new HeuristicTokenEstimator(), _fetcher);
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

    private const string ArticlePage =
        "<html><head><title>2025 Foundry Outlook</title></head>" +
        "<body>" +
        "<nav>Home About Contact Navigation</nav>" +
        "<div class=\"ad\">Buy this advertisement now</div>" +
        "<article><h1>Foundry Report</h1>" +
        "<p>Global foundry capacity grew 12% in 2025.</p></article>" +
        "<footer>Copyright 2026</footer>" +
        "</body></html>";

    // Scenario: Adding a URL fetches and converts the page to text/markdown
    [Fact]
    public void Adding_a_url_fetches_and_converts_the_page_to_text_markdown()
    {
        // Given a page at "https://example.com/foundry" with an article body
        const string url = "https://example.com/foundry";
        _fetcher.WithHtml(url, ArticlePage);

        // When I add the URL "https://example.com/foundry"
        var resource = _service.AddUrl(_projectId, url);

        // Then a resource of type "url" exists
        var stored = _service.Get(resource.Id);
        Assert.NotNull(stored);
        Assert.Equal("url", stored!.Type);

        // And its source_uri is "https://example.com/foundry"
        Assert.Equal(url, stored.SourceUri);

        // And its extracted text is the readable content converted to markdown
        var extracted = _service.GetExtractedText(resource.Id);
        Assert.Contains("# Foundry Report", extracted);
        Assert.Contains("Global foundry capacity grew 12% in 2025.", extracted);
    }

    // Scenario: The original URL is retained as provenance
    [Fact]
    public void The_original_url_is_retained_as_provenance()
    {
        // Given I add the URL "https://example.com/report?id=42"
        const string url = "https://example.com/report?id=42";
        _fetcher.WithHtml(url, ArticlePage);

        // When the resource is saved
        var resource = _service.AddUrl(_projectId, url);

        // Then its source_uri is exactly "https://example.com/report?id=42"
        Assert.Equal(url, _service.Get(resource.Id)!.SourceUri);
    }

    // Scenario: Page title becomes the default resource title
    [Fact]
    public void Page_title_becomes_the_default_resource_title()
    {
        // Given a page whose title is "2025 Foundry Outlook"
        const string url = "https://example.com/outlook";
        _fetcher.WithHtml(url, ArticlePage);

        // When I add its URL
        var resource = _service.AddUrl(_projectId, url);

        // Then the resource title defaults to "2025 Foundry Outlook"
        Assert.Equal("2025 Foundry Outlook", _service.Get(resource.Id)!.Title);
    }

    // Scenario: Boilerplate is stripped from the converted content
    [Fact]
    public void Boilerplate_is_stripped_from_the_converted_content()
    {
        // Given a page with navigation, ads, and an article body
        const string url = "https://example.com/foundry";
        _fetcher.WithHtml(url, ArticlePage);

        // When I add its URL
        var resource = _service.AddUrl(_projectId, url);
        var extracted = _service.GetExtractedText(resource.Id);

        // Then the extracted text contains the article body
        Assert.Contains("Global foundry capacity grew 12% in 2025.", extracted);

        // And it excludes the navigation and ad boilerplate
        Assert.DoesNotContain("Home About Contact", extracted);
        Assert.DoesNotContain("advertisement", extracted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Copyright", extracted);
    }

    // Scenario: A URL resource stores extracted text and records size and token estimate  (@integration)
    [Fact]
    public void A_url_resource_stores_extracted_text_and_records_size_and_token_estimate()
    {
        // Given a reachable page with a known body
        const string url = "https://example.com/foundry";
        _fetcher.WithHtml(url, ArticlePage);

        // When I add its URL
        var resource = _service.AddUrl(_projectId, url);
        var stored = _service.Get(resource.Id)!;

        // Then extracted text is written to the resource's "extracted.txt"
        var expectedExtracted = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, "extracted.txt");
        Assert.True(File.Exists(expectedExtracted));
        Assert.Contains("Global foundry capacity grew 12% in 2025.", File.ReadAllText(expectedExtracted, Encoding.UTF8));

        // And its byte_size and token_estimate are positive
        Assert.NotNull(stored.ByteSize);
        Assert.True(stored.ByteSize > 0);
        Assert.NotNull(stored.TokenEstimate);
        Assert.True(stored.TokenEstimate > 0);
    }

    // Scenario: Content is converted at add-time, not re-fetched on preview
    [Fact]
    public void Content_is_converted_at_add_time_not_re_fetched_on_preview()
    {
        // Given a URL resource added while online
        const string url = "https://example.com/foundry";
        _fetcher.WithHtml(url, ArticlePage);
        var resource = _service.AddUrl(_projectId, url);
        Assert.Equal(1, _fetcher.FetchCount);

        // When I later preview it while offline
        var preview = _service.GetExtractedText(resource.Id);

        // Then the previously converted text is shown without a network call
        Assert.Contains("Global foundry capacity grew 12% in 2025.", preview);
        Assert.Equal(1, _fetcher.FetchCount);
    }

    // Scenario: A malformed URL is rejected — service throws and creates no resource.
    // (The inline-validation-error clause is covered at the view-model level in App.Tests.)
    [Fact]
    public void A_malformed_url_is_rejected_and_creates_no_resource()
    {
        // When I try to add "not-a-url"
        Assert.Throws<ArgumentException>(() => _service.AddUrl(_projectId, "not-a-url"));

        // And no resource is created
        Assert.Empty(_service.List(_projectId));
    }

    // Scenario Outline: Fetch failures surface an actionable error and create no resource
    [Theory]
    [InlineData("connection error")]
    [InlineData("HTTP 404")]
    [InlineData("HTTP 500")]
    [InlineData("timeout")]
    public void Fetch_failures_surface_an_actionable_error_and_create_no_resource(string condition)
    {
        // Given the fetcher will respond with "<condition>"
        const string url = "https://example.com/x";
        _fetcher.WithResult(url, condition switch
        {
            "connection error" => new UrlFetchResult(UrlFetchOutcome.ConnectionError, null, null, null),
            "HTTP 404" => new UrlFetchResult(UrlFetchOutcome.HttpError, 404, "text/html", "<html></html>"),
            "HTTP 500" => new UrlFetchResult(UrlFetchOutcome.HttpError, 500, "text/html", "<html></html>"),
            "timeout" => new UrlFetchResult(UrlFetchOutcome.Timeout, null, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(condition)),
        });

        // When I add the URL "https://example.com/x" / Then I see a human-readable error for "<condition>"
        var ex = Assert.Throws<UrlResourceException>(() => _service.AddUrl(_projectId, url));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        // the error names the specific failure so it is actionable
        switch (condition)
        {
            case "HTTP 404":
                Assert.Contains("404", ex.Message);
                break;
            case "HTTP 500":
                Assert.Contains("500", ex.Message);
                break;
            case "timeout":
                Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
                break;
            case "connection error":
                Assert.Contains("connect", ex.Message, StringComparison.OrdinalIgnoreCase);
                break;
        }

        // And no resource is created
        Assert.Empty(_service.List(_projectId));
    }

    // Scenario: A page with no extractable text reports an empty-content error
    [Fact]
    public void A_page_with_no_extractable_text_reports_an_empty_content_error()
    {
        // Given a page whose body has no readable text
        const string url = "https://example.com/empty";
        _fetcher.WithHtml(url, "<html><head><title>Empty</title></head><body><nav>menu</nav><script>var x=1;</script></body></html>");

        // When I add its URL / Then I see a message that no readable content was found
        var ex = Assert.Throws<UrlResourceException>(() => _service.AddUrl(_projectId, url));
        Assert.Contains("no readable content", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And no resource is created
        Assert.Empty(_service.List(_projectId));
    }
}

using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of docs/features/file-upload-extraction/tests.md. The @unit and
/// @integration scenarios run in the headless gate; they build real fixture files in a temp
/// directory and a temp SQLite store, exercising the genuine extractor libraries (TESTING-STRATEGY
/// §4). Background: "a project 'Semiconductors 2026' is open" — created per test.
/// </summary>
public sealed class FileUploadResourceTests : IDisposable
{
    private const string ReadableMarker = "READABLE";

    private readonly string _dataDir;
    private readonly string _sourceDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;

    public FileUploadResourceTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "mr-file-upload-tests", Guid.NewGuid().ToString("N"));
        _dataDir = Path.Combine(root, "data");
        _sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(_sourceDir);

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
            var root = Directory.GetParent(_dataDir)!.FullName;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // Scenario Outline: Uploading a supported file extracts text and keeps the original
    [Theory]
    [InlineData("pdf", "Foundry filing")]
    [InlineData("docx", "Analyst brief")]
    [InlineData("txt", "Raw notes")]
    [InlineData("md", "Methodology")]
    [InlineData("csv", "Shipments 2025")]
    [InlineData("xlsx", "Forecast model")]
    public void Uploading_a_supported_file_extracts_text_and_keeps_the_original(string ext, string name)
    {
        var filePath = BuildReadableFixture(ext, name);

        // When I upload a "<ext>" file "<name>"
        var result = _service.AddFile(_projectId, filePath);

        // Then a resource "<name>" of type "file" exists
        var stored = _service.Get(result.Resource.Id);
        Assert.NotNull(stored);
        Assert.Equal(name, stored!.Title);
        Assert.Equal("file", stored.Type);
        Assert.Contains(_service.List(_projectId), r => r.Id == stored.Id);

        // And its original blob is stored under
        // "projects/{projectId}/resources/{resourceId}/original.<ext>"
        var expectedBlob = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, $"original.{ext}");
        Assert.True(File.Exists(expectedBlob), $"expected blob at {expectedBlob}");

        // And its extracted text is stored under the resource's "extracted.txt"
        var expectedExtracted = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, "extracted.txt");
        Assert.True(File.Exists(expectedExtracted));

        // And the extracted text contains the document's readable content
        Assert.Contains(ReadableMarker, _service.GetExtractedText(stored.Id));
    }

    // Scenario Outline: Tabular files extract row/column structure as text
    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public void Tabular_files_extract_row_column_structure_as_text(string ext)
    {
        // Given a "<ext>" file with columns "Segment, 2025, 2026" and two data rows
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Segment", "2025", "2026" },
            new[] { "Foundry", "100", "120" },
            new[] { "Memory", "80", "95" },
        };

        var filePath = ext == "csv"
            ? FileFixtures.WriteCsv(_sourceDir, "Shipments", rows)
            : FileFixtures.WriteXlsx(_sourceDir, "Shipments", new[] { ("Data", (IReadOnlyList<IReadOnlyList<string>>)rows) });

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var text = _service.GetExtractedText(result.Resource.Id);

        // Then the extracted text preserves the header and both rows in a readable tabular form
        Assert.Contains("Segment", text);
        Assert.Contains("2025", text);
        Assert.Contains("2026", text);
        Assert.Contains("Foundry", text);
        Assert.Contains("Memory", text);
        Assert.Contains("100", text);
        Assert.Contains("95", text);
        // readable tabular form: columns are separated (not concatenated)
        Assert.Contains("Segment | 2025 | 2026", text);
    }

    // Scenario: XLSX with multiple sheets extracts each sheet
    [Fact]
    public void Xlsx_with_multiple_sheets_extracts_each_sheet()
    {
        // Given an "xlsx" file with sheets "Summary" and "Detail"
        var sheets = new[]
        {
            ("Summary", (IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>> { new[] { "SummaryValue" } }),
            ("Detail", (IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>> { new[] { "DetailValue" } }),
        };
        var filePath = FileFixtures.WriteXlsx(_sourceDir, "Workbook", sheets);

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var text = _service.GetExtractedText(result.Resource.Id);

        // Then the extracted text includes content from both "Summary" and "Detail"
        Assert.Contains("Summary", text);
        Assert.Contains("SummaryValue", text);
        Assert.Contains("Detail", text);
        Assert.Contains("DetailValue", text);
    }

    // Scenario: An uploaded file records its source name, byte size, and token estimate
    [Fact]
    public void An_uploaded_file_records_its_source_name_byte_size_and_token_estimate()
    {
        // Given a "pdf" file "Foundry filing.pdf"
        var filePath = FileFixtures.WritePdf(_sourceDir, "Foundry filing", "Global foundry capacity grew in 2026");
        var fileSize = new FileInfo(filePath).Length;

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var stored = _service.Get(result.Resource.Id)!;

        // Then the resource's source_uri references the original file name
        Assert.NotNull(stored.SourceUri);
        Assert.Contains("Foundry filing.pdf", stored.SourceUri!);

        // And its byte_size equals the uploaded file's size
        Assert.Equal(fileSize, stored.ByteSize);

        // And its token_estimate is a positive number
        Assert.NotNull(stored.TokenEstimate);
        Assert.True(stored.TokenEstimate > 0);
    }

    // Scenario: The original blob is copied into the project, not referenced in place
    [Fact]
    public void The_original_blob_is_copied_into_the_project_not_referenced_in_place()
    {
        // Given a "docx" file located outside the project data directory
        var filePath = FileFixtures.WriteDocx(_sourceDir, "Analyst brief", new[] { "External brief content" });
        Assert.StartsWith(_sourceDir, filePath);

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var stored = _service.Get(result.Resource.Id)!;

        // Then a copy is stored under the resource's directory
        var blob = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, "original.docx");
        Assert.True(File.Exists(blob));
        Assert.StartsWith(_store.DataDirectory, blob);

        // And deleting the external source file does not affect the resource
        File.Delete(filePath);
        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(blob));
        Assert.Contains("External brief content", _service.GetExtractedText(stored.Id));
    }

    // Scenario: An unsupported file type is rejected
    [Fact]
    public void An_unsupported_file_type_is_rejected()
    {
        // Given the "Add resource" menu is open / When I try to upload a ".pptx" file
        var filePath = FileFixtures.WritePlainText(_sourceDir, "Deck", "pptx", "not really a deck");

        // Then I see a message that the type is not supported
        var ex = Assert.Throws<UnsupportedFileTypeException>(() => _service.AddFile(_projectId, filePath));
        Assert.Contains("not supported", ex.Message);

        // And no resource is created
        Assert.Empty(_service.List(_projectId));
    }

    // Scenario: A corrupt or unreadable document surfaces an extraction-failed state
    [Fact]
    public void A_corrupt_or_unreadable_document_surfaces_an_extraction_failed_state()
    {
        // Given a "pdf" file whose contents cannot be parsed
        var filePath = FileFixtures.WriteCorruptPdf(_sourceDir, "Broken filing");

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var stored = _service.Get(result.Resource.Id)!;

        // Then the resource is created with its original blob stored
        var blob = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, "original.pdf");
        Assert.True(File.Exists(blob));
        Assert.Contains(_service.List(_projectId), r => r.Id == stored.Id);

        // And its extraction status is "failed" with a human-readable reason
        Assert.Equal(ExtractionStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));

        // And I am offered a "re-extract" recovery action
        Assert.True(result.CanReExtract);
    }

    // Scenario: A scanned/image-only PDF with no text layer extracts empty text without crashing
    [Fact]
    public void A_scanned_image_only_pdf_extracts_empty_text_without_crashing()
    {
        // Given a "pdf" file that contains only scanned images
        var filePath = FileFixtures.WriteImageOnlyPdf(_sourceDir, "Scanned filing");

        // When I upload it
        var result = _service.AddFile(_projectId, filePath);
        var stored = _service.Get(result.Resource.Id)!;

        // Then the resource is created with the original stored
        var blob = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, "original.pdf");
        Assert.True(File.Exists(blob));

        // And the extracted text is empty
        Assert.Equal("", _service.GetExtractedText(stored.Id));
        Assert.Equal(ExtractionStatus.Empty, result.Status);

        // And a hint suggests adding it as an image resource for vision
        Assert.NotNull(result.Hint);
        Assert.Contains("image", result.Hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vision", result.Hint!, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildReadableFixture(string ext, string name) => ext switch
    {
        "txt" or "md" => FileFixtures.WritePlainText(_sourceDir, name, ext, $"{ReadableMarker} methodology notes for {name}"),
        "csv" => FileFixtures.WriteCsv(_sourceDir, name, new List<IReadOnlyList<string>>
        {
            new[] { "Segment", "Value" },
            new[] { "Foundry", ReadableMarker },
        }),
        "xlsx" => FileFixtures.WriteXlsx(_sourceDir, name, new[]
        {
            ("Sheet1", (IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>>
            {
                new[] { "Segment", "Value" },
                new[] { "Foundry", ReadableMarker },
            }),
        }),
        "docx" => FileFixtures.WriteDocx(_sourceDir, name, new[] { $"{ReadableMarker} analyst brief" }),
        "pdf" => FileFixtures.WritePdf(_sourceDir, name, $"{ReadableMarker} foundry filing"),
        _ => throw new ArgumentOutOfRangeException(nameof(ext), ext, "Unhandled fixture extension."),
    };
}

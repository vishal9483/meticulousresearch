using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Vision;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of docs/features/image-vision-caption/tests.md (SPEC §3.2.1). Images
/// are understood via native vision with no OCR library: the add/store/preview path is offline, and
/// the optional caption cache is driven through a mocked <see cref="IImageCaptioner"/> seam standing
/// in for ai-gateway's <c>IChatService</c> (M2). Background: a project "Semiconductors 2026" is open;
/// the AI gateway is a mocked captioner. Tests build real fixture images in a temp directory and a
/// temp SQLite store, so they run in the headless gate with no network.
/// </summary>
public sealed class ImageVisionCaptionTests : IDisposable
{
    private readonly string _dataDir;
    private readonly string _sourceDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly string _projectId;

    public ImageVisionCaptionTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "mr-image-vision-tests", Guid.NewGuid().ToString("N"));
        _dataDir = Path.Combine(root, "data");
        _sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(_sourceDir);

        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
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

    private ResourceService NewService(FakeImageCaptioner? captioner = null, bool captionOnAdd = false) =>
        new(
            _store,
            new HeuristicTokenEstimator(),
            captioner ?? new FakeImageCaptioner(),
            new ImageCaptionOptions { CaptionOnAdd = captionOnAdd });

    // Scenario Outline: Adding a supported image stores the original in the project
    [Theory]
    [Trait("integration", "true")]
    [InlineData("png", "Revenue chart")]
    [InlineData("jpg", "Filing scan")]
    [InlineData("jpeg", "Booth photo")]
    [InlineData("gif", "Trend animation")]
    [InlineData("webp", "Dashboard shot")]
    public void Adding_a_supported_image_stores_the_original_in_the_project(string ext, string name)
    {
        var service = NewService();
        var filePath = ImageFixtures.Write(_sourceDir, name, ext);
        var expectedByteSize = new FileInfo(filePath).Length;

        // When I add an image "<name>" of type "<ext>"
        var resource = service.AddImage(_projectId, filePath);

        // Then a resource "<name>" of type "image" exists
        var stored = service.Get(resource.Id);
        Assert.NotNull(stored);
        Assert.Equal(name, stored!.Title);
        Assert.Equal("image", stored.Type);

        // And its original is stored under
        // "projects/{projectId}/resources/{resourceId}/original.<ext>"
        var expectedOriginal = Path.Combine(
            _store.DataDirectory, "projects", _projectId, "resources", stored.Id, $"original.{ext}");
        Assert.Equal(expectedOriginal, stored.BlobPath);
        Assert.True(File.Exists(expectedOriginal), $"expected original at {expectedOriginal}");

        // And its byte_size equals the image file size
        Assert.Equal(expectedByteSize, stored.ByteSize);
    }

    // Scenario: An unsupported image type is rejected
    [Fact]
    public void An_unsupported_image_type_is_rejected()
    {
        var service = NewService();
        var filePath = ImageFixtures.WriteRawBytes(_sourceDir, "scan", "bmp", new byte[] { 0x42, 0x4D, 1, 2, 3, 4 });

        // When I try to add an image of type "bmp"
        var ex = Assert.Throws<UnsupportedImageTypeException>(() => service.AddImage(_projectId, filePath));

        // Then I see a message that the type is not supported
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And no resource is created
        Assert.Empty(service.List(_projectId));
    }

    // Scenario: No OCR or external vision library is used at add-time
    [Fact]
    public void No_OCR_or_external_vision_library_is_used_at_add_time()
    {
        var captioner = new FakeImageCaptioner { Result = "should not be called" };
        // caption-on-add disabled: adding must not perform any text understanding at add-time.
        var service = NewService(captioner, captionOnAdd: false);
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // Given I add a PNG image resource
        var resource = service.AddImage(_projectId, filePath);

        // Then no OCR/text-extraction library is invoked (no vision/caption call was made either)
        Assert.Equal(0, captioner.Calls);

        // And any text understanding is deferred to the model at request time (no extracted text now)
        Assert.Equal("", service.GetExtractedText(resource.Id));
        Assert.True(string.IsNullOrEmpty(service.Get(resource.Id)!.ExtractedText));
    }

    // Scenario: An enabled image is assembled as an image content block at request time
    [Fact]
    public void An_enabled_image_is_assembled_as_an_image_content_block_at_request_time()
    {
        var service = NewService();
        var filePath = ImageFixtures.Write(_sourceDir, "Revenue chart", "png");
        var resource = service.AddImage(_projectId, filePath);
        Assert.True(service.Get(resource.Id)!.Enabled); // an enabled image resource

        // When the request context is assembled for a generation
        var assembler = new VisionContentAssembler();
        var block = assembler.Assemble(service.Get(resource.Id)!);

        // Then the image is included as an image content block referencing the stored original
        Assert.Equal(resource.Id, block.ResourceId);
        Assert.Equal(resource.BlobPath, block.SourcePath);
        Assert.Equal("image/png", block.MediaType);

        // And its bytes are inlined (base64) at that time, not stored inline in the DB
        var expectedBase64 = Convert.ToBase64String(File.ReadAllBytes(resource.BlobPath!));
        Assert.Equal(expectedBase64, block.Base64Data);

        var persisted = service.Get(resource.Id)!;
        Assert.True(string.IsNullOrEmpty(persisted.ExtractedText)); // no base64/body persisted inline
        Assert.DoesNotContain(expectedBase64, persisted.ExtractedText ?? "");
        Assert.Equal(resource.BlobPath, persisted.BlobPath); // DB holds a path, not the bytes
    }

    // Scenario: Image tokens count toward the context budget
    [Fact]
    public void Image_tokens_count_toward_the_context_budget()
    {
        var service = NewService();
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png", width: 120, height: 80);

        // Given an enabled image resource
        var resource = service.AddImage(_projectId, filePath);

        // When the pre-send estimate is computed
        var estimate = service.Get(resource.Id)!.TokenEstimate;

        // Then the image contributes an estimated token amount to the total
        Assert.NotNull(estimate);
        Assert.True(estimate > 0, "an image must contribute a positive token estimate");
    }

    // Scenario: On add, a short caption is generated and stored as extracted text
    [Fact]
    public void On_add_a_short_caption_is_generated_and_stored_as_extracted_text()
    {
        const string caption = "Bar chart of 2025 foundry revenue by region";
        // Given caption-on-add is enabled; And the mocked IChatService returns the caption ...
        var captioner = new FakeImageCaptioner { Result = caption };
        var service = NewService(captioner, captionOnAdd: true);
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // When I add a PNG image resource
        var resource = service.AddImage(_projectId, filePath);

        // Then one small vision call is made
        Assert.Equal(1, captioner.Calls);

        // And the resource's extracted text is the caption
        Assert.Equal(caption, service.GetExtractedText(resource.Id));
        Assert.Equal(caption, service.Get(resource.Id)!.ExtractedText);
    }

    // Scenario: The cached caption makes the image findable and previewable without resending it
    [Fact]
    public void The_cached_caption_makes_the_image_findable_and_previewable_without_resending_it()
    {
        const string caption = "Bar chart of 2025 foundry revenue by region";
        var captioner = new FakeImageCaptioner { Result = caption };
        var addService = NewService(captioner, captionOnAdd: true);
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // Given an image resource with cached caption "..."
        var resource = addService.AddImage(_projectId, filePath);
        Assert.Equal(caption, addService.Get(resource.Id)!.ExtractedText);

        // A fresh service used purely for preview must make no vision call to display it.
        var previewCaptioner = new FakeImageCaptioner { Result = "should not be called" };
        var previewService = NewService(previewCaptioner, captionOnAdd: true);

        // When I preview the resource
        var previewText = previewService.GetExtractedText(resource.Id);

        // Then I see the caption text (shown alongside a thumbnail in the UI)
        Assert.Equal(caption, previewText);

        // And no vision call is made to display the preview
        Assert.Equal(0, previewCaptioner.Calls);
    }

    // Scenario: Caption generation is optional and failure does not block adding the image
    [Fact]
    public void Caption_generation_failure_does_not_block_adding_the_image()
    {
        // Given caption-on-add is enabled; And the vision call fails
        var captioner = new FakeImageCaptioner { Throw = true };
        var service = NewService(captioner, captionOnAdd: true);
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // When I add an image resource
        var resource = service.AddImage(_projectId, filePath);

        // Then the image resource is still created with its original stored
        var stored = service.Get(resource.Id);
        Assert.NotNull(stored);
        Assert.True(File.Exists(stored!.BlobPath));

        // And its extracted text is empty
        Assert.Equal("", service.GetExtractedText(resource.Id));
        Assert.True(string.IsNullOrEmpty(stored.ExtractedText));

        // And I can trigger caption generation later
        var laterCaptioner = new FakeImageCaptioner { Result = "Recovered caption" };
        var laterService = NewService(laterCaptioner, captionOnAdd: true);
        var updated = laterService.GenerateImageCaption(resource.Id);
        Assert.Equal(1, laterCaptioner.Calls);
        Assert.Equal("Recovered caption", updated.ExtractedText);
        Assert.Equal("Recovered caption", laterService.GetExtractedText(resource.Id));
    }

    // Scenario: With caption-on-add disabled, no vision call is made on add
    [Fact]
    public void With_caption_on_add_disabled_no_vision_call_is_made_on_add()
    {
        // Given caption-on-add is disabled
        var captioner = new FakeImageCaptioner { Result = "should not be called" };
        var service = NewService(captioner, captionOnAdd: false);
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // When I add an image resource
        var resource = service.AddImage(_projectId, filePath);

        // Then no vision call is made
        Assert.Equal(0, captioner.Calls);

        // And the resource is created with empty extracted text
        Assert.Equal("", service.GetExtractedText(resource.Id));
        Assert.True(string.IsNullOrEmpty(service.Get(resource.Id)!.ExtractedText));
    }

    // Scenario: Selecting a non-vision model with image resources in scope warns the user
    [Fact]
    public void Selecting_a_non_vision_model_with_image_resources_in_scope_warns_the_user()
    {
        var service = NewService();
        var filePath = ImageFixtures.Write(_sourceDir, "chart", "png");

        // Given an enabled image resource is in scope
        service.AddImage(_projectId, filePath);
        var enabled = service.ListEnabled(_projectId);
        Assert.Contains(enabled, r => r.Type == ResourceTypes.Image);

        // And the selected model does not accept image input
        const bool modelAcceptsImages = false;

        // When I attempt a generation
        var decision = VisionScopeGuard.Evaluate(enabled, modelAcceptsImages);

        // Then I am warned and offered to switch to a vision-capable model
        Assert.True(decision.ShouldWarn);
        Assert.True(decision.OffersModelSwitch);

        // And the generation does not silently drop the image
        Assert.False(decision.ImageSilentlyDropped);
    }

    /// <summary>Records calls so tests can assert exactly how many vision calls were made.</summary>
    private sealed class FakeImageCaptioner : IImageCaptioner
    {
        public int Calls { get; private set; }
        public string? Result { get; init; }
        public bool Throw { get; init; }

        public string Caption(string imagePath)
        {
            Calls++;
            if (Throw)
                throw new InvalidOperationException("vision call failed");
            return Result ?? "";
        }
    }
}

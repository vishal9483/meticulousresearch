using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;
using AdvancingClock = MeticulousResearch.Core.Tests.Turns.AdvancingClock;

namespace MeticulousResearch.Core.Tests.Conversations;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/image-attachments/tests.md
/// (SPEC §3.2.1, §3.6). These are @unit and run in the headless gate; they touch a temp SQLite
/// database (TESTING-STRATEGY §4) and drive generation through the scripted <see cref="FakeChatService"/>.
/// Per-turn image attachments are message content — never project resources.
/// </summary>
public sealed class ImageAttachmentServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly MessageAttachmentStore _attachmentStore;
    private readonly ConversationService _service;

    public ImageAttachmentServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-image-attachment-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _attachmentStore = new MessageAttachmentStore(new ProjectFileStore(_dataDir));
        _service = new ConversationService(
            _store, _chat, _projects, _resources, _clock,
            new ConversationGroundingAssembler(), _attachmentStore);
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

    // Scenario: A sent turn includes the image as a vision content block alongside the text
    [Fact]
    public async Task A_sent_turn_includes_the_image_as_a_vision_content_block_alongside_the_text()
    {
        // Given a pending turn with text and one image attachment
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        var attachment = ImageAttachment.FromBytes("chart.png", SamplePng.Bytes);
        _chat.WithCompletionText("reply").WithUsage(10, 5);

        // When I send the turn
        await _service.Ask(
            conversation.Id, "What does this chart show?", "claude-opus-5",
            resourceScope: null, attachments: new[] { attachment });

        // Then the request to the backend contains the user text
        Assert.NotNull(_chat.LastRequest);
        Assert.Equal("What does this chart show?", _chat.LastRequest!.UserMessage);

        // And an image content block for the attachment
        Assert.Single(_chat.LastRequest.UserImages);
        var block = _chat.LastRequest.UserImages[0];
        Assert.Equal(attachment.Id, block.ResourceId);
        Assert.Equal("image/png", block.MediaType);
        Assert.Equal(Convert.ToBase64String(SamplePng.Bytes), block.Base64Data);
    }

    // Scenario: An attached image is not created as a project resource
    [Fact]
    public async Task An_attached_image_is_not_created_as_a_project_resource()
    {
        // Given a project with 0 resources
        var project = _projects.Create("P");
        Assert.Empty(_resources.List(project.Id));
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("reply").WithUsage(10, 5);

        // When I send a turn with an image attachment
        var attachment = ImageAttachment.FromBytes("chart.png", SamplePng.Bytes);
        await _service.Ask(
            conversation.Id, "look at this", "claude-opus-5",
            resourceScope: null, attachments: new[] { attachment });

        // Then the project still has 0 resources
        Assert.Empty(_resources.List(project.Id));

        // And the image is stored as message content, not as a resource
        var userMessage = _service.GetMessages(conversation.Id)
            .First(m => m.Role == ConversationService.UserRole);
        var stored = _attachmentStore.Get(project.Id, userMessage.Id);
        Assert.Single(stored);
        Assert.Equal("chart.png", stored[0].FileName);
        Assert.Equal(SamplePng.Bytes, stored[0].Bytes);
    }

    // Scenario: Multiple images can be attached to a single turn
    [Fact]
    public async Task Multiple_images_can_be_attached_to_a_single_turn()
    {
        // Given a composer with two images attached
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("reply").WithUsage(10, 5);
        var one = ImageAttachment.FromBytes("a.png", SamplePng.Bytes);
        var two = ImageAttachment.FromBytes("b.png", SamplePng.Bytes);

        // When I send the turn
        await _service.Ask(
            conversation.Id, "compare these", "claude-opus-5",
            resourceScope: null, attachments: new[] { one, two });

        // Then the request contains two image content blocks
        Assert.NotNull(_chat.LastRequest);
        Assert.Equal(2, _chat.LastRequest!.UserImages.Count);
        Assert.Equal(one.Id, _chat.LastRequest.UserImages[0].ResourceId);
        Assert.Equal(two.Id, _chat.LastRequest.UserImages[1].ResourceId);
    }

    // Scenario: Image tokens count toward the turn's input tokens and cost
    [Fact]
    public async Task Image_tokens_count_toward_the_turns_input_tokens_and_cost()
    {
        // Given a turn with text and an image attachment
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        var attachment = ImageAttachment.FromBytes("chart.png", SamplePng.Bytes);

        // And the backend reports input tokens that include the image's token cost
        const long backendInputTokens = 2_500;
        _chat.WithCompletionText("reply").WithUsage(backendInputTokens, 300);

        // When the turn completes
        var assistant = await _service.Ask(
            conversation.Id, "text", "claude-sonnet-5",
            resourceScope: null, attachments: new[] { attachment });

        // Then the recorded input tokens include the image contribution (the backend authoritative count)
        Assert.Equal(backendInputTokens, assistant.TokensIn);

        // And the per-turn cost reflects those input tokens
        var calculator = new CatalogTurnCostCalculator(ModelCatalogLoader.Default);
        var breakdown = calculator.Calculate(TurnMetadata.FromMessage(assistant));
        var price = ModelCatalogLoader.Default.GetPrice("claude-sonnet-5")!.Value;
        var expectedInputCost = backendInputTokens / 1_000_000d * price.InputMTok;
        Assert.True(breakdown.InputCost > 0, "input cost should reflect the recorded input tokens");
        Assert.Equal(expectedInputCost, breakdown.InputCost, 6);
    }
}

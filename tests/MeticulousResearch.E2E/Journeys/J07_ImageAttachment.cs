using System.Linq;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Models;
using MeticulousResearch.E2E.Support;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-07 — Image attachment in-thread (vision), covers SPEC §3.2.1: a multimodal message, not a
/// persistent resource. The composer thumbnail is a window concern; the truths — the image is sent
/// as a vision content block (not created as a project resource) and a non-vision model produces an
/// advisory switch warning — run headlessly.
/// </summary>
public sealed class J07_ImageAttachment : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J07_ImageAttachment() => _projectId = _h.Projects.Create("EV Market 2026").Id;

    public void Dispose() => _h.Dispose();

    // @e2e
    // Scenario: Ana attaches a chart directly to a message and asks about it
    [Fact]
    public async Task Ana_attaches_a_chart_directly_to_a_message_and_asks_about_it()
    {
        // Given a conversation using a vision-capable model.
        Assert.True(_h.Models.IsVisionCapable("claude-opus-5"));
        var conversation = _h.Conversations.Create(_projectId);

        // When I attach an image to a new message and ask about it.
        var attachment = ImageAttachment.FromBytes("scan.jpg", SamplePng.Bytes);
        _h.Chat.WithCompletionText("The chart shows an upward trend.").WithUsage(120, 30);
        await _h.Conversations.Ask(
            conversation.Id, "What trend does this chart show?", "claude-opus-5",
            resourceScope: null, attachments: new[] { attachment });

        // Then the model receives the image as a vision content block alongside the text.
        Assert.NotNull(_h.Chat.LastRequest);
        Assert.Equal("What trend does this chart show?", _h.Chat.LastRequest!.UserMessage);
        var block = Assert.Single(_h.Chat.LastRequest.UserImages);
        Assert.Equal(attachment.Id, block.ResourceId);
        Assert.StartsWith("image/", block.MediaType);

        // And the image is stored as message content, never as a project resource (§3.2.1).
        Assert.Empty(_h.Resources.List(_projectId));
        var userMessage = _h.Conversations.GetMessages(conversation.Id).First(m => m.Role == "user");
        Assert.Single(_h.AttachmentStore.Get(_projectId, userMessage.Id));

        // And the turn's usage/cost is recorded (image tokens count toward the billed turn).
        Assert.True(_h.Cost.GetConversationCost(conversation.Id).Cost > 0m);
    }

    // @e2e @unit
    // Scenario: Selecting a non-vision model warns before sending an image.
    // (The shipped catalog is all-vision, so a small catalog with a non-vision model exercises the
    // advisor faithfully.)
    [Fact]
    public void Selecting_a_non_vision_model_warns_before_sending_an_image()
    {
        var catalog = new ModelCatalog(
            tiers: new[]
            {
                new ModelInfo { Id = "text-only", Name = "Text Only", Tier = "Fast", Vision = false },
                new ModelInfo { Id = "vision-model", Name = "Vision Model", Tier = "Deep", Vision = true },
            },
            additional: Array.Empty<ModelInfo>(),
            defaultModelId: "text-only");

        // When I select a model that does not accept image input, with an image in scope.
        var warning = ModelVisionAdvisor.Advise(catalog, "text-only", imageInScope: true);

        // Then the app warns and offers to switch to a vision-capable model.
        Assert.NotNull(warning);
        Assert.Equal("vision-model", warning!.SuggestedVisionModelId);

        // And no warning is shown for a vision-capable model.
        Assert.Null(ModelVisionAdvisor.Advise(catalog, "vision-model", imageInScope: true));
    }
}

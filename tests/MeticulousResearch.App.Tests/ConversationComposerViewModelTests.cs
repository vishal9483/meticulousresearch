using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// Faithful @unit translation of the composer/vision/estimate scenarios in
/// docs/features/image-attachments/tests.md (SPEC §3.2.1, §3.6). Exercises the window-free
/// <see cref="ConversationComposerViewModel"/>: paste/attach/remove of per-turn images, the
/// pre-send "estimated" image-token contribution, and the non-vision warning + switch offer.
/// </summary>
public sealed class ConversationComposerViewModelTests
{
    private const string CatalogJson = """
    {
      "defaultModel": "claude-opus-5",
      "tiers": [
        { "tier": "Deep", "name": "Claude Opus 5", "id": "claude-opus-5", "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5, "priceOutputMTok": 25, "vision": true }
      ],
      "additional": [
        { "name": "Legacy Text Only", "id": "legacy-text-only", "contextTokens": 200000, "maxOutputTokens": 64000, "priceInputMTok": 1, "priceOutputMTok": 5, "vision": false }
      ]
    }
    """;

    private static ConversationComposerViewModel NewComposer(IModelCatalog? catalog = null)
    {
        var cat = catalog ?? ModelCatalogLoader.Default;
        return new ConversationComposerViewModel(
            new ModelPickerViewModel(cat), new HeuristicTokenEstimator());
    }

    // Scenario: Pasting an image into the composer attaches it to the pending turn
    [Fact]
    public void Pasting_an_image_into_the_composer_attaches_it_to_the_pending_turn()
    {
        // Given a conversation composer with the text "What does this chart show?"
        var composer = NewComposer();
        composer.Text = "What does this chart show?";

        // When I paste an image
        composer.PasteImage(SamplePng.Bytes);

        // Then the pending turn carries the text and one image attachment
        Assert.Equal("What does this chart show?", composer.Text);
        Assert.Single(composer.Attachments);
        Assert.True(composer.HasAttachments);
    }

    // Scenario: Attaching an image file adds it to the pending turn
    [Fact]
    public void Attaching_an_image_file_adds_it_to_the_pending_turn()
    {
        // Given a conversation composer
        var composer = NewComposer();

        // When I attach an image file "chart.png"
        composer.AttachImage("chart.png", SamplePng.Bytes);

        // Then the pending turn carries an image attachment for "chart.png"
        Assert.Single(composer.Attachments);
        Assert.Equal("chart.png", composer.Attachments[0].FileName);
    }

    // Scenario: An attachment can be removed before sending
    [Fact]
    public void An_attachment_can_be_removed_before_sending()
    {
        // Given a composer with one image attached
        var composer = NewComposer();
        composer.AttachImage("chart.png", SamplePng.Bytes);
        var attachment = composer.Attachments[0];

        // When I remove the attachment
        composer.RemoveAttachment(attachment);

        // Then the pending turn has no image attachments
        Assert.Empty(composer.Attachments);
        Assert.False(composer.HasAttachments);
    }

    // Scenario: The pre-send estimate includes an image token estimate
    [Fact]
    public void The_pre_send_estimate_includes_an_image_token_estimate()
    {
        // Given a composer with one image attached
        var composer = NewComposer();
        composer.AttachImage("chart.png", SamplePng.Bytes);

        // When the pre-send token estimate is computed
        var estimate = composer.Estimate;

        // Then the estimate includes an estimated image token contribution
        Assert.True(estimate.ImageTokens > 0, "an attached image should contribute estimated tokens");

        // And it is labeled "estimated"
        Assert.Equal("estimated", estimate.Label);
    }

    // Scenario: Attaching an image while a non-vision model is selected warns and offers to switch
    [Fact]
    public void Attaching_an_image_while_a_non_vision_model_is_selected_warns_and_offers_to_switch()
    {
        // Given the selected model has vision=false
        var catalog = ModelCatalogLoader.Load(CatalogJson).Catalog;
        var composer = NewComposer(catalog);
        composer.ModelPicker.SelectModel("legacy-text-only");
        Assert.False(composer.ModelPicker.HasVisionWarning); // no image yet

        // When I attach an image to the turn
        composer.AttachImage("chart.png", SamplePng.Bytes);

        // Then I see a warning that the model cannot read images
        Assert.True(composer.ModelPicker.HasVisionWarning);
        Assert.NotNull(composer.ModelPicker.VisionWarning);
        Assert.Contains("image", composer.ModelPicker.VisionWarning!.Message, StringComparison.OrdinalIgnoreCase);

        // And I am offered to switch to a vision-capable model
        Assert.True(composer.ModelPicker.CanSwitchToVisionModel);
        Assert.True(catalog.IsVisionCapable(composer.ModelPicker.VisionWarning.SuggestedVisionModelId!));
    }
}

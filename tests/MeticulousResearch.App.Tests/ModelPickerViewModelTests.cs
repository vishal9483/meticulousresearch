using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Models;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for <see cref="ModelPickerViewModel"/> (model-selector/tests.md, SPEC §6, §3.2.1).
/// Covers the tiered picker surface (tiers + "All models") and the advisory vision warning/switch
/// when a non-vision model is selected with an image in scope.
/// </summary>
public sealed class ModelPickerViewModelTests
{
    // Scenario: Selecting a non-vision model with an image in scope warns and offers to switch
    //   Given a catalog entry "legacy-text-only" with vision=false
    //   And an image is attached to the turn
    //   When I select "legacy-text-only"
    //   Then I see a warning that the model cannot read images
    //   And I am offered to switch to a vision-capable model
    [Fact]
    public void Selecting_a_non_vision_model_with_an_image_in_scope_warns_and_offers_to_switch()
    {
        // Given a catalog entry "legacy-text-only" with vision=false (alongside a vision-capable model)
        const string json = """
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
        var catalog = ModelCatalogLoader.Load(json).Catalog;
        var picker = new ModelPickerViewModel(catalog);

        // And an image is attached to the turn
        picker.ImageInScope = true;

        // When I select "legacy-text-only"
        picker.SelectModel("legacy-text-only");

        // Then I see a warning that the model cannot read images
        Assert.True(picker.HasVisionWarning);
        Assert.NotNull(picker.VisionWarning);
        Assert.Contains("image", picker.VisionWarning!.Message, StringComparison.OrdinalIgnoreCase);

        // And I am offered to switch to a vision-capable model
        Assert.True(picker.CanSwitchToVisionModel);
        Assert.Equal("claude-opus-5", picker.VisionWarning.SuggestedVisionModelId);
        Assert.True(catalog.IsVisionCapable(picker.VisionWarning.SuggestedVisionModelId!));

        // switching applies the suggested vision-capable model and clears the warning
        picker.SwitchToVisionModelCommand.Execute(null);
        Assert.Equal("claude-opus-5", picker.CurrentModelId);
        Assert.False(picker.HasVisionWarning);
    }

    // A vision-capable selection with an image in scope raises no warning (guards against a
    // tautological warning that always fires).
    [Fact]
    public void A_vision_capable_model_with_an_image_in_scope_raises_no_warning()
    {
        var picker = new ModelPickerViewModel(ModelCatalogLoader.Default) { ImageInScope = true };

        picker.SelectModel("Deep"); // claude-opus-5, vision=true

        Assert.False(picker.HasVisionWarning);
        Assert.Null(picker.VisionWarning);
    }

    // A non-vision selection with NO image in scope raises no warning (the warning is scoped to
    // "an image is attached").
    [Fact]
    public void A_non_vision_model_without_an_image_raises_no_warning()
    {
        const string json = """
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
        var picker = new ModelPickerViewModel(ModelCatalogLoader.Load(json).Catalog);

        picker.SelectModel("legacy-text-only");

        Assert.False(picker.HasVisionWarning);
    }

    // The picker surfaces the friendly tiers and the "All models" (additional) list (underpins the
    // @ui picker scenario).
    [Fact]
    public void The_picker_exposes_the_friendly_tiers_and_the_all_models_list()
    {
        var picker = new ModelPickerViewModel(ModelCatalogLoader.Default);

        Assert.Equal(
            new[] { "Frontier", "Deep", "Balanced", "Fast" },
            picker.Tiers.Select(t => t.Tier).ToArray());
        Assert.Contains(picker.AdditionalModels, m => m.Id == "claude-opus-4-8");
        Assert.Contains(picker.AdditionalModels, m => m.Id == "claude-sonnet-4-5");
    }
}

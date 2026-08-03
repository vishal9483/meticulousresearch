using MeticulousResearch.Core.Models;

namespace MeticulousResearch.Core.Tests.Models;

/// <summary>
/// Faithful xUnit translation of the config-driven catalog @unit scenarios in
/// docs/features/model-selector/tests.md (SPEC §6.1–§6.3). These are @unit and run in the headless
/// gate. The catalog owns the tier→id mapping, the "All models" list, prices, and fallback semantics.
/// </summary>
public sealed class ModelCatalogTests
{
    // Scenario: The default catalog loads the shipped tiers
    //   Given the app with its default model catalog
    //   When I read the available tiers
    //   Then the tiers are "Frontier", "Deep", "Balanced", and "Fast"
    //   And each tier maps to a concrete model id, context window, max output, and prices
    [Fact]
    public void The_default_catalog_loads_the_shipped_tiers()
    {
        var catalog = ModelCatalogLoader.Default;

        Assert.Equal(
            new[] { "Frontier", "Deep", "Balanced", "Fast" },
            catalog.Tiers.Select(t => t.Tier).ToArray());

        foreach (var tier in catalog.Tiers)
        {
            Assert.False(string.IsNullOrWhiteSpace(tier.Id));
            Assert.True(tier.ContextTokens > 0, $"{tier.Tier} should have a context window");
            Assert.True(tier.MaxOutputTokens > 0, $"{tier.Tier} should have a max output");
            Assert.True(tier.PriceInputMTok > 0, $"{tier.Tier} should have an input price");
            Assert.True(tier.PriceOutputMTok > 0, $"{tier.Tier} should have an output price");
        }
    }

    // Scenario Outline: Each default tier maps to the specified model id
    //   Given the default model catalog
    //   When I resolve the "<tier>" tier
    //   Then the model id is "<id>"
    [Theory]
    [InlineData("Frontier", "claude-fable-5")]
    [InlineData("Deep", "claude-opus-5")]
    [InlineData("Balanced", "claude-sonnet-5")]
    [InlineData("Fast", "claude-haiku-4-5")]
    public void Each_default_tier_maps_to_the_specified_model_id(string tier, string id)
    {
        var catalog = ModelCatalogLoader.Default;

        var model = catalog.Resolve(tier);

        Assert.NotNull(model);
        Assert.Equal(id, model!.Id);
    }

    // Scenario: Additional (non-tier) models are available in the "All models" list
    //   Given the default model catalog
    //   When I read the "All models" list
    //   Then it includes "claude-opus-4-8", "claude-opus-4-7", "claude-sonnet-4-6", and "claude-sonnet-4-5"
    [Fact]
    public void Additional_models_are_available_in_the_all_models_list()
    {
        var catalog = ModelCatalogLoader.Default;

        var ids = catalog.AdditionalModels.Select(m => m.Id).ToArray();

        Assert.Contains("claude-opus-4-8", ids);
        Assert.Contains("claude-opus-4-7", ids);
        Assert.Contains("claude-sonnet-4-6", ids);
        Assert.Contains("claude-sonnet-4-5", ids);
    }

    // Scenario: The catalog is overridable without a rebuild
    //   Given a custom catalog JSON that adds a model "claude-mythos-5"
    //   When the app loads the catalog
    //   Then "claude-mythos-5" is selectable
    //   And its prices come from the JSON
    [Fact]
    public void The_catalog_is_overridable_without_a_rebuild()
    {
        const string customJson = """
        {
          "defaultModel": "claude-opus-5",
          "tiers": [
            { "tier": "Deep", "name": "Claude Opus 5", "id": "claude-opus-5", "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5, "priceOutputMTok": 25, "vision": true }
          ],
          "additional": [
            { "name": "Claude Mythos 5", "id": "claude-mythos-5", "contextTokens": 500000, "maxOutputTokens": 64000, "priceInputMTok": 12, "priceOutputMTok": 60, "vision": true }
          ]
        }
        """;

        var result = ModelCatalogLoader.Load(customJson);

        Assert.Null(result.Warning);
        // "claude-mythos-5" is selectable
        var mythos = result.Catalog.TryGet("claude-mythos-5");
        Assert.NotNull(mythos);
        Assert.Same(mythos, result.Catalog.Resolve("claude-mythos-5"));
        // its prices come from the JSON
        var price = result.Catalog.GetPrice("claude-mythos-5");
        Assert.NotNull(price);
        Assert.Equal(12, price!.Value.InputMTok);
        Assert.Equal(60, price.Value.OutputMTok);
    }

    // Scenario: A malformed catalog JSON falls back to the shipped default with a clear warning
    //   Given a catalog file that is not valid JSON
    //   When the app loads the catalog
    //   Then the shipped default catalog is used
    //   And a human-readable warning is surfaced (no stack trace)
    [Fact]
    public void A_malformed_catalog_falls_back_to_the_shipped_default_with_a_clear_warning()
    {
        var result = ModelCatalogLoader.Load("this is not valid JSON {");

        // the shipped default catalog is used
        Assert.True(result.UsedFallback);
        Assert.Equal(ModelCatalogLoader.Default.DefaultModelId, result.Catalog.DefaultModelId);
        Assert.Equal(
            ModelCatalogLoader.Default.Tiers.Select(t => t.Id),
            result.Catalog.Tiers.Select(t => t.Id));

        // a human-readable warning is surfaced (no stack trace)
        Assert.False(string.IsNullOrWhiteSpace(result.Warning));
        Assert.DoesNotContain("   at ", result.Warning!);
        Assert.DoesNotContain(nameof(System.Text.Json.JsonException), result.Warning!);
    }

    // Scenario: The default project model is Claude Opus 5
    //   Given the default model catalog
    //   When I read the default model
    //   Then it is "claude-opus-5"
    [Fact]
    public void The_default_project_model_is_claude_opus_5()
    {
        Assert.Equal("claude-opus-5", ModelCatalogLoader.Default.DefaultModelId);
    }
}

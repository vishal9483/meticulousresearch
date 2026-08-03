using MeticulousResearch.Core.Models;

namespace MeticulousResearch.Core.Turns;

/// <summary>
/// A pure per-turn cost calculator (SPEC §3.6) priced from the <see cref="IModelCatalog"/>. Input and
/// output tokens are priced at the model's per-MTok rates; prompt-cache tokens are priced relative to
/// the input rate using the standard Anthropic multipliers (cache-write 1.25×, cache-read 0.1×), so
/// the expanded breakdown itemises all four token classes. An unknown model prices at zero rather than
/// throwing. This is the default seam <c>cost-tracking</c> (M4) later replaces authoritatively.
/// </summary>
public sealed class CatalogTurnCostCalculator : ITurnCostCalculator
{
    /// <summary>Prompt-cache write price as a multiple of the input price (Anthropic pricing).</summary>
    public const double CacheWriteMultiplier = 1.25;

    /// <summary>Prompt-cache read price as a multiple of the input price (Anthropic pricing).</summary>
    public const double CacheReadMultiplier = 0.1;

    private const double TokensPerMTok = 1_000_000d;

    private readonly IModelCatalog _catalog;

    /// <summary>Creates the calculator over the model catalog it prices from.</summary>
    /// <param name="catalog">The model catalog (owned by <c>model-selector</c>).</param>
    /// <exception cref="ArgumentNullException">The catalog is null.</exception>
    public CatalogTurnCostCalculator(IModelCatalog catalog)
        => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public TurnCostBreakdown Calculate(TurnMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var price = metadata.Model is null ? null : _catalog.GetPrice(metadata.Model);
        var inputMTok = price?.InputMTok ?? 0d;
        var outputMTok = price?.OutputMTok ?? 0d;

        return new TurnCostBreakdown(
            Cost(metadata.InputTokens, inputMTok),
            Cost(metadata.OutputTokens, outputMTok),
            Cost(metadata.CacheReadTokens, inputMTok * CacheReadMultiplier),
            Cost(metadata.CacheWriteTokens, inputMTok * CacheWriteMultiplier));
    }

    private static double Cost(long tokens, double priceMTok) => tokens / TokensPerMTok * priceMTok;
}

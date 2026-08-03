using MeticulousResearch.Core.Models;

namespace MeticulousResearch.Core.Cost;

/// <summary>
/// Supplies the per-model <see cref="CostRates"/> from which <c>cost-tracking</c> computes cost
/// (SPEC §3.6, §6.3). A model absent from the source returns <c>null</c> so its usage is flagged
/// <c>unknown-price</c> rather than counted as $0.00. The production source adapts the model catalog
/// (owned by <c>model-selector</c>); tests inject a fixed price table.
/// </summary>
public interface ICostPriceSource
{
    /// <summary>Returns the prices for <paramref name="model"/>, or <c>null</c> when it is unknown.</summary>
    /// <param name="model">The concrete model id.</param>
    CostRates? GetRates(string? model);
}

/// <summary>
/// An in-memory, mutable <see cref="ICostPriceSource"/> backed by a model-id → rates dictionary.
/// A price change reprices historical usage because totals recompute from stored tokens against the
/// current table (SPEC §3.6), which this source models via <see cref="SetRates"/>.
/// </summary>
public sealed class DictionaryCostPriceSource : ICostPriceSource
{
    private readonly Dictionary<string, CostRates> _rates = new(StringComparer.Ordinal);

    /// <summary>Sets (or replaces) the rates for a model id.</summary>
    /// <param name="model">The concrete model id.</param>
    /// <param name="rates">The per-MTok rates for that model.</param>
    public void SetRates(string model, CostRates rates)
    {
        ArgumentNullException.ThrowIfNull(model);
        _rates[model] = rates;
    }

    /// <inheritdoc />
    public CostRates? GetRates(string? model)
        => model is not null && _rates.TryGetValue(model, out var r) ? r : null;
}

/// <summary>
/// The production <see cref="ICostPriceSource"/> that adapts the <see cref="IModelCatalog"/> (owned
/// by <c>model-selector</c>, §6.3). Input and output rates come from <see cref="ModelPrice"/>;
/// prompt-cache rates are derived from the input rate using the standard Anthropic multipliers
/// (cache-read 0.1×, cache-write 1.25×) — the same multipliers the default per-turn calculator uses,
/// so cache-read/write are priced consistently until the catalog carries explicit cache columns.
/// </summary>
public sealed class CatalogCostPriceSource : ICostPriceSource
{
    /// <summary>Prompt-cache read price as a multiple of the input price (Anthropic pricing).</summary>
    public const decimal CacheReadMultiplier = 0.1m;

    /// <summary>Prompt-cache write price as a multiple of the input price (Anthropic pricing).</summary>
    public const decimal CacheWriteMultiplier = 1.25m;

    private readonly IModelCatalog _catalog;

    /// <summary>Creates the source over the catalog it prices from.</summary>
    /// <param name="catalog">The model catalog (owned by <c>model-selector</c>).</param>
    /// <exception cref="ArgumentNullException">The catalog is null.</exception>
    public CatalogCostPriceSource(IModelCatalog catalog)
        => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public CostRates? GetRates(string? model)
    {
        if (model is null)
            return null;
        var price = _catalog.GetPrice(model);
        if (price is null)
            return null;
        var input = (decimal)price.Value.InputMTok;
        return new CostRates(
            input,
            (decimal)price.Value.OutputMTok,
            input * CacheReadMultiplier,
            input * CacheWriteMultiplier);
    }
}

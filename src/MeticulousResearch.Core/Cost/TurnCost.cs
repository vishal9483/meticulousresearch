namespace MeticulousResearch.Core.Cost;

/// <summary>
/// The itemised USD cost of a single turn (SPEC §3.6): the contribution of each of the four billed
/// token classes plus their <see cref="Total"/>. When the turn's model is absent from the price
/// catalog the cost is <see cref="UnknownPrice"/> — surfaced but excluded from priced totals rather
/// than silently counted as $0.00. Money is a <see cref="decimal"/> so fractional cents survive;
/// rounding happens only at the display layer.
/// </summary>
/// <param name="InputCost">USD cost of the billed input tokens.</param>
/// <param name="OutputCost">USD cost of the billed output tokens.</param>
/// <param name="CacheReadCost">USD cost of the prompt-cache read tokens.</param>
/// <param name="CacheWriteCost">USD cost of the prompt-cache write tokens.</param>
/// <param name="UnknownPrice">Whether the turn's model had no catalog price (excluded from totals).</param>
public readonly record struct TurnCost(
    decimal InputCost,
    decimal OutputCost,
    decimal CacheReadCost,
    decimal CacheWriteCost,
    bool UnknownPrice)
{
    /// <summary>The total USD cost of the turn (sum of the four contributions).</summary>
    public decimal Total => InputCost + OutputCost + CacheReadCost + CacheWriteCost;

    /// <summary>An unknown-price cost for a model absent from the catalog (all components zero, flagged).</summary>
    public static TurnCost Unknown => new(0m, 0m, 0m, 0m, UnknownPrice: true);
}

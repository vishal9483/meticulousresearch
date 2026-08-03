namespace MeticulousResearch.Core.Turns;

/// <summary>
/// The itemised per-turn cost (SPEC §3.6): the USD contribution of each billed token class — input,
/// output, prompt-cache read, and prompt-cache write — plus their <see cref="Total"/>. The inline
/// badge shows the total; expanding it reveals these four line items.
/// </summary>
/// <param name="InputCost">USD cost of the billed input tokens.</param>
/// <param name="OutputCost">USD cost of the billed output tokens.</param>
/// <param name="CacheReadCost">USD cost of the prompt-cache read tokens.</param>
/// <param name="CacheWriteCost">USD cost of the prompt-cache write tokens.</param>
public readonly record struct TurnCostBreakdown(
    double InputCost,
    double OutputCost,
    double CacheReadCost,
    double CacheWriteCost)
{
    /// <summary>The total USD cost of the turn (sum of the four contributions).</summary>
    public double Total => InputCost + OutputCost + CacheReadCost + CacheWriteCost;
}

/// <summary>
/// The per-turn cost seam (SPEC §3.6). Computes a turn's <see cref="TurnCostBreakdown"/> from its
/// <see cref="TurnMetadata"/>. The authoritative cost engine is owned by <c>cost-tracking</c> (M4);
/// this feature's badge reads through this seam so that engine swaps in without changing the badge.
/// </summary>
public interface ITurnCostCalculator
{
    /// <summary>Computes the itemised cost of the turn described by <paramref name="metadata"/>.</summary>
    /// <param name="metadata">The turn's model + token metadata.</param>
    TurnCostBreakdown Calculate(TurnMetadata metadata);
}

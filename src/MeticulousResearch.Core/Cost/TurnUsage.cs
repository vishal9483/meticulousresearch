namespace MeticulousResearch.Core.Cost;

/// <summary>
/// The billed token usage of a single turn (SPEC §3.6): the four token classes reported by the API
/// usage fields. Tokens are the ground truth from which cost is always recomputed at current prices,
/// so <c>cost-tracking</c> stores no second authoritative cost.
/// </summary>
/// <param name="InputTokens">Billed input (prompt) tokens.</param>
/// <param name="OutputTokens">Billed output (completion) tokens.</param>
/// <param name="CacheReadTokens">Prompt-cache read tokens.</param>
/// <param name="CacheWriteTokens">Prompt-cache write tokens.</param>
public readonly record struct TurnUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens = 0,
    long CacheWriteTokens = 0)
{
    /// <summary>The total of all four billed token classes.</summary>
    public long TotalTokens => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;
}

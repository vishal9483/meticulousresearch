namespace MeticulousResearch.Core.Budget;

/// <summary>
/// The non-resource portion of the pre-send estimate: a small fixed overhead for custom
/// instructions plus per-message framing (SPEC §8). Kept explicit so the estimate is the sum of the
/// enabled resources plus this overhead, with nothing hidden.
/// </summary>
/// <param name="OverheadTokens">
/// Estimated tokens for custom instructions and message overhead added on top of the enabled
/// resources.
/// </param>
public sealed record ContextBudgetScope(long OverheadTokens)
{
    /// <summary>A scope with no additional overhead (resources only).</summary>
    public static ContextBudgetScope None { get; } = new(0);
}

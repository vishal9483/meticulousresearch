namespace MeticulousResearch.Core.Budget;

/// <summary>
/// A computed pre-send context-budget estimate (SPEC §3.2, §8): the enabled resources' individual
/// contributions plus a fixed overhead, the resulting total, the thresholds it was checked against
/// (the configured budget and the selected model's context window), and the derived status. Every
/// value here is an <em>estimate</em> and is surfaced labeled "estimated"; authoritative token
/// counts come from API usage after a send (SPEC §3.6).
/// </summary>
/// <param name="Contributions">Per-resource token contributions from the enabled resources.</param>
/// <param name="OverheadTokens">The fixed instruction/message overhead included in the total.</param>
/// <param name="TotalTokens">The estimated total: enabled resources + overhead.</param>
/// <param name="WindowTokens">The selected model's context window (hard ceiling).</param>
/// <param name="BudgetTokens">The configured context budget (soft threshold).</param>
/// <param name="Status">Whether the estimate is ok, over budget, or over window.</param>
public sealed record ContextBudgetEstimate(
    IReadOnlyList<ResourceContribution> Contributions,
    long OverheadTokens,
    long TotalTokens,
    long WindowTokens,
    long BudgetTokens,
    ContextBudgetStatus Status)
{
    /// <summary>The label under which these numbers must be shown — always an estimate (SPEC §3.6).</summary>
    public string Label => "estimated";

    /// <summary>True — this is an estimate, never an authoritative count.</summary>
    public bool IsEstimated => true;

    /// <summary>Whether a warning should be shown (any status other than <see cref="ContextBudgetStatus.Ok"/>).</summary>
    public bool HasWarning => Status != ContextBudgetStatus.Ok;

    /// <summary>
    /// The warning message matching the status — one of "none", "over configured budget", or
    /// "over model context window".
    /// </summary>
    public string WarningMessage => Status switch
    {
        ContextBudgetStatus.OverBudget => "over configured budget",
        ContextBudgetStatus.OverWindow => "over model context window",
        _ => "none",
    };

    /// <summary>
    /// The enabled resources ordered by their token contribution, largest first — the deselect
    /// guidance shown when over budget (SPEC §3.2).
    /// </summary>
    public IReadOnlyList<ResourceContribution> LargestContributors =>
        Contributions.OrderByDescending(c => c.Tokens).ToList();
}

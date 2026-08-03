namespace MeticulousResearch.Core.Cost;

/// <summary>
/// The cost-computation contract for the whole app (SPEC §3.6). Owns cost at three levels — per
/// turn, per conversation, and consolidated per project — computed from the stored <b>token</b>
/// counts against the <b>current</b> catalog prices, so a price change reprices historical usage
/// with no token mutation. The audit-only <c>cost_usd</c> snapshot on a turn is never used in totals.
/// Downstream, <c>usage-csv-export</c> consumes <see cref="GetPricedTurns"/>.
/// </summary>
public interface ICostService
{
    /// <summary>
    /// Pure per-turn cost (SPEC §3.6): <c>input×priceIn + output×priceOut + cacheRead×priceCacheRead
    /// + cacheWrite×priceCacheWrite</c>, prices per MTok from the catalog. A model absent from the
    /// catalog yields <see cref="TurnCost.UnknownPrice"/> rather than a $0.00 cost.
    /// </summary>
    /// <param name="usage">The turn's billed token usage.</param>
    /// <param name="model">The turn's model id.</param>
    TurnCost ComputeTurnCost(TurnUsage usage, string? model);

    /// <summary>
    /// The running total (cost + tokens) over a conversation's completed assistant turns (SPEC §3.6).
    /// A conversation with no completed turns totals $0.00.
    /// </summary>
    /// <param name="conversationId">The conversation id.</param>
    CostTotal GetConversationCost(string conversationId);

    /// <summary>
    /// The consolidated project cost for a time window (SPEC §3.6, §9.1(7)): total plus breakdowns by
    /// source (conversations vs artifacts), by model, and by the injected-clock window.
    /// </summary>
    /// <param name="projectId">The project id.</param>
    /// <param name="window">The time window to bucket into (defaults to all-time).</param>
    ConsolidatedCost GetProjectCost(string projectId, CostWindow window = CostWindow.AllTime);

    /// <summary>
    /// Every priced turn row for a project (all-time), recomputed from stored tokens at current
    /// prices. Consumed by <c>usage-csv-export</c>.
    /// </summary>
    /// <param name="projectId">The project id.</param>
    IReadOnlyList<PricedTurnRecord> GetPricedTurns(string projectId);

    /// <summary>
    /// Evaluates the soft monthly budget for a project against its month-to-date spend plus a new
    /// turn's cost (SPEC §3.6). Always non-blocking — a warning, never a refusal; the turn is
    /// recorded regardless. A disabled budget never raises a warning.
    /// </summary>
    /// <param name="projectId">The project id.</param>
    /// <param name="budget">The configured budget (use <see cref="ProjectBudget.Off"/> when none).</param>
    /// <param name="newTurnCost">The cost of the turn that just completed.</param>
    BudgetEvaluation EvaluateBudget(string projectId, ProjectBudget budget, decimal newTurnCost);
}

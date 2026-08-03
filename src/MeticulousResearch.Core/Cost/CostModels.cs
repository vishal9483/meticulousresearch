namespace MeticulousResearch.Core.Cost;

/// <summary>Which kind of usage produced a cost row (SPEC §3.6 consolidated breakdown by source).</summary>
public enum CostSource
{
    /// <summary>A conversation turn (an assistant <c>Message</c>).</summary>
    Conversation,

    /// <summary>An artifact-version generation (an <c>ArtifactVersion</c>).</summary>
    Artifact,
}

/// <summary>The time window a consolidated total is bucketed into, relative to the injected clock (SPEC §3.6).</summary>
public enum CostWindow
{
    /// <summary>Usage dated on the clock's current (UTC) date.</summary>
    Today,

    /// <summary>Usage within the rolling 7 days ending at the clock's instant.</summary>
    Week,

    /// <summary>All recorded usage, regardless of date.</summary>
    AllTime,
}

/// <summary>
/// A running total of cost and tokens over a set of completed turns (SPEC §3.6). Cost is the sum of
/// the priced turns; tokens are the sum of every billed token class. Unknown-price turns contribute
/// tokens but no cost (they are surfaced via <see cref="UnknownPriceCount"/>, never as $0.00).
/// </summary>
/// <param name="Cost">Total USD cost over priced turns.</param>
/// <param name="Tokens">Total billed tokens over all counted turns.</param>
/// <param name="UnknownPriceCount">Number of counted turns whose model had no catalog price.</param>
public readonly record struct CostTotal(decimal Cost, long Tokens, int UnknownPriceCount = 0);

/// <summary>
/// The consolidated project cost (SPEC §3.6, §9.1(7)) for a time window: the total plus the three
/// breakdowns — by source (conversations vs artifacts), by model, and the window it covers. Totals
/// are recomputed from stored tokens at current prices; unknown-price usage is excluded from the
/// priced totals and reported via <see cref="UnknownPriceCount"/>.
/// </summary>
/// <param name="Window">The window this total covers.</param>
/// <param name="Total">Total USD cost across both sources in the window.</param>
/// <param name="Conversations">USD cost of conversation turns in the window.</param>
/// <param name="Artifacts">USD cost of artifact-version generations in the window.</param>
/// <param name="ByModel">USD cost per model id in the window.</param>
/// <param name="TotalTokens">Total billed tokens across the window.</param>
/// <param name="UnknownPriceCount">Number of turns excluded from the priced total for want of a price.</param>
public sealed record ConsolidatedCost(
    CostWindow Window,
    decimal Total,
    decimal Conversations,
    decimal Artifacts,
    IReadOnlyDictionary<string, decimal> ByModel,
    long TotalTokens,
    int UnknownPriceCount);

/// <summary>
/// One priced turn row (SPEC §3.6) consumed by <c>usage-csv-export</c>: the turn's identity, source,
/// model, token usage, the cost recomputed from current prices (<c>null</c> when unknown-price), and
/// its timestamp. Every persisted completed turn is <see cref="IsAuthoritative"/> — the token counts
/// come from the API usage fields, never from a pre-send local estimate.
/// </summary>
/// <param name="TurnId">The message or artifact-version id.</param>
/// <param name="Source">Whether this is a conversation turn or an artifact generation.</param>
/// <param name="ConversationId">Owning conversation id (conversation turns), else <c>null</c>.</param>
/// <param name="ArtifactId">Owning artifact id (artifact generations), else <c>null</c>.</param>
/// <param name="Model">The model id, or <c>null</c> when unrecorded.</param>
/// <param name="Usage">The billed token usage of the turn.</param>
/// <param name="Cost">The USD cost recomputed at current prices, or <c>null</c> when unknown-price.</param>
/// <param name="UnknownPrice">Whether the model had no catalog price.</param>
/// <param name="IsAuthoritative">Whether the tokens are authoritative API usage (always true here).</param>
/// <param name="Timestamp">The turn's creation instant.</param>
/// <param name="SnapshotCostUsd">The audit-only cost snapshotted at completion (never used in totals).</param>
public sealed record PricedTurnRecord(
    string TurnId,
    CostSource Source,
    string? ConversationId,
    string? ArtifactId,
    string? Model,
    TurnUsage Usage,
    decimal? Cost,
    bool UnknownPrice,
    bool IsAuthoritative,
    DateTimeOffset Timestamp,
    double? SnapshotCostUsd);

/// <summary>
/// A per-project soft monthly budget (SPEC §3.6): a non-blocking spend threshold. Off by default —
/// a project with no configured budget uses <see cref="Off"/>, which never raises a warning.
/// </summary>
/// <param name="Enabled">Whether the budget guardrail is active.</param>
/// <param name="MonthlyLimitUsd">The monthly USD threshold above which a warning is raised.</param>
public readonly record struct ProjectBudget(bool Enabled, decimal MonthlyLimitUsd)
{
    /// <summary>The default: no budget configured, so no warning is ever raised.</summary>
    public static ProjectBudget Off => new(Enabled: false, MonthlyLimitUsd: 0m);
}

/// <summary>
/// The outcome of evaluating a soft monthly budget against a project's month-to-date spend plus a
/// new turn (SPEC §3.6). Always non-blocking: <see cref="Exceeded"/> is a warning, and the turn is
/// still recorded regardless.
/// </summary>
/// <param name="Exceeded">Whether the projected month-to-date total exceeds the enabled budget.</param>
/// <param name="MonthToDateCost">The project's cost so far this month, before the new turn.</param>
/// <param name="ProjectedCost">Month-to-date plus the new turn's cost.</param>
/// <param name="LimitUsd">The configured limit, or <c>null</c> when the budget is off.</param>
public readonly record struct BudgetEvaluation(
    bool Exceeded,
    decimal MonthToDateCost,
    decimal ProjectedCost,
    decimal? LimitUsd);

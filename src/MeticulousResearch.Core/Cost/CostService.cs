using System.Globalization;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Cost;

/// <summary>
/// The authoritative cost engine (SPEC §3.6) over the <see cref="DataStore"/>. Follows the Core
/// repository pattern: short-lived <see cref="AppDbContext"/> reads, time windows bucketed against an
/// injected <see cref="IClock"/>, and prices resolved through an <see cref="ICostPriceSource"/>.
/// Every total is recomputed from stored <b>token</b> columns at <b>current</b> prices, so a price
/// change reprices history with no token mutation; the <c>cost_usd</c> snapshot is read for audit
/// only and never mixed into totals.
/// </summary>
public sealed class CostService : ICostService
{
    private const decimal TokensPerMTok = 1_000_000m;
    private const string AssistantRole = "assistant";

    private readonly DataStore _store;
    private readonly ICostPriceSource _prices;
    private readonly IClock _clock;

    /// <summary>Creates the cost service over its collaborators.</summary>
    /// <param name="store">The data store to read tokens from.</param>
    /// <param name="prices">The price source (catalog-backed in production).</param>
    /// <param name="clock">The injected clock for time-window bucketing.</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    public CostService(DataStore store, ICostPriceSource prices, IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public TurnCost ComputeTurnCost(TurnUsage usage, string? model)
    {
        var rates = _prices.GetRates(model);
        if (rates is null)
            return TurnCost.Unknown;

        var r = rates.Value;
        return new TurnCost(
            Component(usage.InputTokens, r.InputMTok),
            Component(usage.OutputTokens, r.OutputMTok),
            Component(usage.CacheReadTokens, r.CacheReadMTok),
            Component(usage.CacheWriteTokens, r.CacheWriteMTok),
            UnknownPrice: false);
    }

    /// <inheritdoc />
    public CostTotal GetConversationCost(string conversationId)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        using var db = _store.CreateDbContext();
        var messages = db.Messages
            .Where(m => m.ConversationId == conversationId && m.Role == AssistantRole)
            .ToList();

        decimal cost = 0m;
        long tokens = 0;
        int unknown = 0;
        foreach (var m in messages)
        {
            var usage = UsageOf(m);
            tokens += usage.TotalTokens;
            var turn = ComputeTurnCost(usage, m.Model);
            if (turn.UnknownPrice)
                unknown++;
            else
                cost += turn.Total;
        }

        return new CostTotal(cost, tokens, unknown);
    }

    /// <inheritdoc />
    public ConsolidatedCost GetProjectCost(string projectId, CostWindow window = CostWindow.AllTime)
    {
        var rows = GetPricedTurns(projectId)
            .Where(r => InWindow(r.Timestamp, window))
            .ToList();

        decimal conversations = 0m;
        decimal artifacts = 0m;
        long tokens = 0;
        int unknown = 0;
        var byModel = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            tokens += r.Usage.TotalTokens;
            if (r.Cost is null)
            {
                unknown++;
                continue;
            }

            var value = r.Cost.Value;
            if (r.Source == CostSource.Conversation)
                conversations += value;
            else
                artifacts += value;

            var key = r.Model ?? string.Empty;
            byModel[key] = byModel.TryGetValue(key, out var existing) ? existing + value : value;
        }

        return new ConsolidatedCost(
            window,
            conversations + artifacts,
            conversations,
            artifacts,
            byModel,
            tokens,
            unknown);
    }

    /// <inheritdoc />
    public IReadOnlyList<PricedTurnRecord> GetPricedTurns(string projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        using var db = _store.CreateDbContext();

        var conversationIds = db.Conversations
            .Where(c => c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToList();
        var artifactIds = db.Artifacts
            .Where(a => a.ProjectId == projectId)
            .Select(a => a.Id)
            .ToList();

        var records = new List<PricedTurnRecord>();

        var messages = db.Messages
            .Where(m => conversationIds.Contains(m.ConversationId) && m.Role == AssistantRole)
            .ToList();
        foreach (var m in messages)
        {
            var usage = UsageOf(m);
            var turn = ComputeTurnCost(usage, m.Model);
            records.Add(new PricedTurnRecord(
                m.Id,
                CostSource.Conversation,
                m.ConversationId,
                ArtifactId: null,
                m.Model,
                usage,
                turn.UnknownPrice ? null : turn.Total,
                turn.UnknownPrice,
                IsAuthoritative: true,
                ParseTimestamp(m.CreatedAt),
                m.CostUsd));
        }

        var versions = db.ArtifactVersions
            .Where(v => artifactIds.Contains(v.ArtifactId) && v.Model != null)
            .ToList();
        foreach (var v in versions)
        {
            var usage = new TurnUsage(v.TokensIn, v.TokensOut);
            var turn = ComputeTurnCost(usage, v.Model);
            records.Add(new PricedTurnRecord(
                v.Id,
                CostSource.Artifact,
                ConversationId: null,
                v.ArtifactId,
                v.Model,
                usage,
                turn.UnknownPrice ? null : turn.Total,
                turn.UnknownPrice,
                IsAuthoritative: true,
                ParseTimestamp(v.CreatedAt),
                v.CostUsd));
        }

        return records;
    }

    /// <inheritdoc />
    public BudgetEvaluation EvaluateBudget(string projectId, ProjectBudget budget, decimal newTurnCost)
    {
        var monthToDate = GetPricedTurns(projectId)
            .Where(r => r.Cost is not null && InCurrentMonth(r.Timestamp))
            .Sum(r => r.Cost!.Value);

        var projected = monthToDate + newTurnCost;
        if (!budget.Enabled)
            return new BudgetEvaluation(Exceeded: false, monthToDate, projected, LimitUsd: null);

        return new BudgetEvaluation(
            projected > budget.MonthlyLimitUsd,
            monthToDate,
            projected,
            budget.MonthlyLimitUsd);
    }

    private static decimal Component(long tokens, decimal priceMTok) => tokens / TokensPerMTok * priceMTok;

    private static TurnUsage UsageOf(Message m)
        => new(m.TokensIn, m.TokensOut, m.TokensCacheRead, m.TokensCacheWrite);

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private bool InWindow(DateTimeOffset ts, CostWindow window)
    {
        var now = _clock.UtcNow;
        return window switch
        {
            CostWindow.Today => ts.UtcDateTime.Date == now.UtcDateTime.Date,
            CostWindow.Week => ts >= now.AddDays(-7),
            _ => true,
        };
    }

    private bool InCurrentMonth(DateTimeOffset ts)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var local = ts.UtcDateTime;
        return local.Year == now.Year && local.Month == now.Month;
    }
}

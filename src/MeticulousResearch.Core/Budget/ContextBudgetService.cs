using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.Core.Budget;

/// <summary>
/// The pre-send context-budget estimator (SPEC §3.2, §8). Builds the estimate on top of the
/// enabled-resource scope (<see cref="IResourceService.ListEnabled"/>) — the single source of truth
/// for what generation includes — rather than a parallel sum, adds the fixed instruction/message
/// overhead, and evaluates the total against the model's context window (hard ceiling) and the
/// configured budget (soft threshold, from <see cref="ISettingsService.ContextBudget"/>). The
/// window overage dominates so it can never be ignored: the caller must deselect resources or
/// switch to a larger-window model, never truncate silently.
/// </summary>
public sealed class ContextBudgetService : IContextBudgetService
{
    private readonly IResourceService _resources;
    private readonly ISettingsService _settings;

    /// <summary>Creates the service over the enabled-resource scope and app settings.</summary>
    public ContextBudgetService(IResourceService resources, ISettingsService settings)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public ContextBudgetEstimate Estimate(string projectId, ContextBudgetScope scope, ModelWindow model)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(model);

        var contributions = _resources.ListEnabled(projectId)
            .Select(r => new ResourceContribution(r.Id, r.Title, r.TokenEstimate ?? 0))
            .ToList();

        var overhead = scope.OverheadTokens;
        var total = contributions.Sum(c => c.Tokens) + overhead;
        long budget = _settings.ContextBudget;

        var status = Evaluate(total, model.ContextTokens, budget);
        return new ContextBudgetEstimate(contributions, overhead, total, model.ContextTokens, budget, status);
    }

    /// <summary>
    /// Classifies a total against the window and budget. The model window is the hard ceiling and
    /// takes precedence: a total over the window is <see cref="ContextBudgetStatus.OverWindow"/>
    /// even when it is also over budget.
    /// </summary>
    public static ContextBudgetStatus Evaluate(long total, long window, long budget)
    {
        if (total > window)
            return ContextBudgetStatus.OverWindow;
        if (total > budget)
            return ContextBudgetStatus.OverBudget;
        return ContextBudgetStatus.Ok;
    }
}

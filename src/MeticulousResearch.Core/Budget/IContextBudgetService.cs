namespace MeticulousResearch.Core.Budget;

/// <summary>
/// The pre-send context-budget contract (SPEC §3.2, §8). Before every send, estimate the token
/// usage of the enabled resources plus a fixed overhead, compare it against the selected model's
/// context window and the configured budget, and return a breakdown that lets the UI warn and help
/// the user deselect — never silently truncating (the caller must resolve an over-window estimate
/// by deselecting or switching model before generation proceeds).
/// </summary>
public interface IContextBudgetService
{
    /// <summary>
    /// Computes the pre-send estimate for a project's enabled resources against the given model
    /// window and the configured budget.
    /// </summary>
    /// <param name="projectId">The project whose enabled resources form the scope.</param>
    /// <param name="scope">The fixed instruction/message overhead to include.</param>
    /// <param name="model">The selected model whose context window is the hard ceiling.</param>
    /// <returns>The estimate breakdown, thresholds, and status.</returns>
    ContextBudgetEstimate Estimate(string projectId, ContextBudgetScope scope, ModelWindow model);
}

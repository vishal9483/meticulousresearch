namespace MeticulousResearch.Core.Budget;

/// <summary>
/// The pre-send status of a context-budget estimate against the two thresholds (SPEC §3.2, §8):
/// the user-configured budget (soft) and the selected model's context window (hard ceiling — no
/// silent truncation). The window overage always dominates the softer budget overage.
/// </summary>
public enum ContextBudgetStatus
{
    /// <summary>The estimate is within both the configured budget and the model window.</summary>
    Ok,

    /// <summary>The estimate exceeds the configured budget but is still within the model window.</summary>
    OverBudget,

    /// <summary>The estimate exceeds the model's context window (the hard ceiling).</summary>
    OverWindow,
}

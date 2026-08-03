namespace MeticulousResearch.Core.Resources;

/// <summary>
/// Produces a deterministic token estimate for a piece of text, used to populate
/// <c>Resource.token_estimate</c> for context-budget planning (SPEC §3.2, §3.5). Injected so the
/// <c>token-estimation</c> feature can refine the heuristic without touching resource code; the
/// same input must always yield the same estimate.
/// </summary>
public interface ITokenEstimator
{
    /// <summary>Estimates the number of tokens the given text will consume.</summary>
    long Estimate(string text);
}

namespace MeticulousResearch.Core.Budget;

/// <summary>
/// The selected model's context window used as the hard ceiling for the pre-send estimate
/// (SPEC §6.3, §8). Downstream <c>model-selector</c> (M2) resolves this from the config-driven
/// model catalog (its <c>contextTokens</c>); this feature consumes it as a plain value so switching
/// models simply re-resolves the window.
/// </summary>
/// <param name="ModelId">The selected model's catalog id.</param>
/// <param name="ContextTokens">The model's context window in tokens (the hard ceiling).</param>
public sealed record ModelWindow(string ModelId, long ContextTokens);

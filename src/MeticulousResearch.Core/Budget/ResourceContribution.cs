namespace MeticulousResearch.Core.Budget;

/// <summary>
/// One enabled resource's contribution to the pre-send estimate, used to show the user which
/// resources contribute most when they are over budget (SPEC §3.2 — help deselect).
/// </summary>
/// <param name="ResourceId">The contributing resource's id.</param>
/// <param name="Title">The resource's display title.</param>
/// <param name="Tokens">The resource's estimated token contribution.</param>
public sealed record ResourceContribution(string ResourceId, string Title, long Tokens);

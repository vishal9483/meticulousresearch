namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// A human-readable, actionable error surface (SPEC §3.7): the <paramref name="Message"/> shown to
/// the user and the label of the <paramref name="RecoveryAction"/> that lets them recover. Never
/// carries a raw exception message or stack trace — those are logged, not shown.
/// </summary>
/// <param name="Message">The human-readable message (never a raw exception detail).</param>
/// <param name="RecoveryAction">The label of the recovery action button (e.g. "Retry").</param>
public sealed record UserError(string Message, string RecoveryAction);

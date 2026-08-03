namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// Turns a failure into a human-readable, actionable <see cref="UserError"/> (SPEC §3.7). Known
/// failures map to a specific message + recovery action; an unexpected exception maps to a generic
/// message while the raw detail is written to the log — never to the screen.
/// </summary>
public interface IUserErrorMapper
{
    /// <summary>Maps a known, classified failure to its message and recovery action.</summary>
    /// <param name="kind">The classified failure kind.</param>
    UserError Map(UserFacingFailureKind kind);

    /// <summary>
    /// Maps an exception to a user-facing error. A <see cref="UserFacingException"/> maps by its
    /// <see cref="UserFacingException.Kind"/>; anything else is logged and mapped to a generic
    /// message so no raw exception detail reaches the UI.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <param name="context">A short context string for the log entry.</param>
    UserError FromException(Exception exception, string context = "operation");
}

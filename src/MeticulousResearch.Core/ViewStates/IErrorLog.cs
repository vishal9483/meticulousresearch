namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// Sink for the raw detail of an unexpected failure. The error mapper writes the exception here so
/// the screen only ever shows a generic, human-readable message (SPEC §3.7 — never a raw stack
/// trace).
/// </summary>
public interface IErrorLog
{
    /// <summary>Records the raw detail of an unexpected failure, off-screen.</summary>
    /// <param name="context">A short context string identifying where the failure occurred.</param>
    /// <param name="exception">The exception whose detail must not reach the UI.</param>
    void LogUnexpected(string context, Exception exception);
}

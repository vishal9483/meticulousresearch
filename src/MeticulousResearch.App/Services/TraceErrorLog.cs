using System.Diagnostics;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.App.Services;

/// <summary>
/// Default <see cref="IErrorLog"/>: writes the raw detail of an unexpected failure to
/// <see cref="Trace"/> so it is captured off-screen (SPEC §3.7 — never a raw stack trace in the UI).
/// </summary>
public sealed class TraceErrorLog : IErrorLog
{
    /// <inheritdoc />
    public void LogUnexpected(string context, Exception exception)
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        Trace.TraceError($"[{context}] Unexpected failure: {exception}");
    }
}

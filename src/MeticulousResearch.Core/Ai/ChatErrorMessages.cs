namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Canonical, human-readable, actionable error messages surfaced through <see cref="ChatFaulted"/>
/// (SPEC §3.7). Kept in one place so both backends speak identically and never leak a stack trace.
/// </summary>
public static class ChatErrorMessages
{
    /// <summary>Shown when no API key is configured from any source; points the user to Settings.</summary>
    public const string MissingApiKey =
        "No API key is configured. Add your Anthropic API key in Settings to start generating.";

    /// <summary>Shown when a mid-stream interruption makes the turn retryable.</summary>
    public const string InterruptedRetryable =
        "The generation backend was interrupted. Your turn can be retried.";

    /// <summary>Shown when the generation backend is unavailable after repeated failures.</summary>
    public const string BackendUnavailable =
        "The generation backend is unavailable. Check your connection and API key, then try again in a moment.";
}

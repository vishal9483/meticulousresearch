namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The classified reason a <see cref="ChatFaulted"/> occurred. Transient-error classification
/// (429 / 5xx) lives with the gateway so <c>rate-limit-backoff</c> can wrap it without owning the
/// backoff policy (ai-gateway/phase.md).
/// </summary>
public enum ChatErrorKind
{
    /// <summary>No API key was configured from any source; the caller must add one in Settings.</summary>
    MissingApiKey,

    /// <summary>The generation backend could not be started or has repeatedly crashed.</summary>
    BackendUnavailable,

    /// <summary>The API returned HTTP 429 (rate limited); retryable after backoff.</summary>
    RateLimited,

    /// <summary>The API returned an HTTP 5xx server error; retryable after backoff.</summary>
    ServerError,

    /// <summary>A transport-level failure (e.g. the sidecar crashed mid-stream); retryable.</summary>
    Transport,

    /// <summary>An unclassified failure.</summary>
    Unknown,
}

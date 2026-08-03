namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Classifies transient API failures (429 / 5xx) into a <see cref="ChatErrorKind"/> and whether the
/// turn is retryable. Ownership lives in the gateway (ai-gateway/phase.md) so <c>rate-limit-backoff</c>
/// can wrap the classified fault without re-implementing the taxonomy; the backoff <em>policy</em>
/// is not decided here.
/// </summary>
public static class ChatErrorClassifier
{
    /// <summary>
    /// Maps an HTTP status code to a fault. 429 → <see cref="ChatErrorKind.RateLimited"/> (retryable);
    /// 5xx → <see cref="ChatErrorKind.ServerError"/> (retryable); anything else →
    /// <see cref="ChatErrorKind.Unknown"/> (not retryable).
    /// </summary>
    public static (ChatErrorKind Kind, bool Retryable) FromStatusCode(int statusCode) => statusCode switch
    {
        429 => (ChatErrorKind.RateLimited, true),
        >= 500 and <= 599 => (ChatErrorKind.ServerError, true),
        _ => (ChatErrorKind.Unknown, false),
    };
}

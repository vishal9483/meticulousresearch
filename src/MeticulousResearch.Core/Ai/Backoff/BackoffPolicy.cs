namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// The backoff <em>policy</em> owned by <c>rate-limit-backoff</c> (the fault <em>classification</em>
/// lives in <see cref="ChatErrorClassifier"/>). It computes an exponentially growing, jittered delay
/// per retry attempt and honors a server <c>retry-after</c> hint when it is larger than the computed
/// delay (SPEC §8). Attempts are capped at <see cref="MaxAttempts"/>.
/// </summary>
public sealed class BackoffPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly IJitterSource _jitter;

    /// <summary>Creates a policy from its base delay, attempt cap, and jitter source.</summary>
    /// <param name="baseDelay">The delay before the first retry (attempt 1); doubles each attempt.</param>
    /// <param name="maxAttempts">The maximum total number of attempts (the first try plus retries); must be &gt;= 1.</param>
    /// <param name="jitter">The jitter source applied to each computed delay.</param>
    public BackoffPolicy(TimeSpan baseDelay, int maxAttempts, IJitterSource jitter)
    {
        if (baseDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "The base delay must be non-negative.");
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one attempt is required.");
        _baseDelay = baseDelay;
        MaxAttempts = maxAttempts;
        _jitter = jitter ?? throw new ArgumentNullException(nameof(jitter));
    }

    /// <summary>The maximum total number of attempts (the first try plus retries).</summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Computes the delay to wait before the next attempt after the retry numbered
    /// <paramref name="attempt"/> (1-based). The exponential value is <c>baseDelay * 2^(attempt-1)</c>;
    /// equal-jitter is applied so the delay is <c>half + fraction * half</c> of that value. When
    /// <paramref name="retryAfter"/> is present and larger, it overrides the computed delay (SPEC §8).
    /// </summary>
    /// <param name="attempt">The 1-based retry index.</param>
    /// <param name="retryAfter">An optional server-provided <c>retry-after</c> hint.</param>
    /// <returns>The delay to wait before the next attempt.</returns>
    public TimeSpan ComputeDelay(int attempt, TimeSpan? retryAfter = null)
    {
        if (attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(attempt), "The attempt index is 1-based.");

        var exponential = _baseDelay.Ticks * Math.Pow(2, attempt - 1);
        var half = exponential / 2.0;
        var fraction = Math.Clamp(_jitter.NextFraction(), 0.0, 1.0);
        var jittered = TimeSpan.FromTicks((long)(half + fraction * half));

        return retryAfter is { } after && after > jittered ? after : jittered;
    }
}

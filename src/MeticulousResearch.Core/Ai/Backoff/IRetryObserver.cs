namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// Receives the "retrying…" state transitions from <see cref="RetryingChatService"/> so a view-model
/// can show a non-alarming retry indicator with the attempt count (SPEC §8). A retry raises
/// <see cref="OnRetrying"/>; reaching a terminal outcome (success, cancellation, or final failure)
/// raises <see cref="OnResolved"/> so the indicator clears.
/// </summary>
public interface IRetryObserver
{
    /// <summary>Signals that the turn is being retried after a retryable fault.</summary>
    /// <param name="state">The attempt number and the delay before the next try.</param>
    void OnRetrying(RetryState state);

    /// <summary>Signals that the turn reached a terminal outcome; the retry indicator should clear.</summary>
    void OnResolved();
}

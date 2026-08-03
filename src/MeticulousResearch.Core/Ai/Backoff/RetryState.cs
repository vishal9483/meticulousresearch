namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// The "retrying…" signal surfaced to the UI while a turn is being retried after a 429 / 5xx
/// (SPEC §8): the current <paramref name="Attempt"/> number and the <paramref name="NextDelay"/> the
/// backoff will wait before the next try. It is a non-alarming state — not an error — and clears on
/// success or final failure.
/// </summary>
/// <param name="Attempt">The 1-based number of the attempt that just failed and is being retried.</param>
/// <param name="NextDelay">The backoff delay before the next attempt.</param>
public sealed record RetryState(int Attempt, TimeSpan NextDelay);

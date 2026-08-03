namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// Supplies the jitter fraction applied to a computed backoff delay so retries after a 429 / 5xx do
/// not stampede in lock-step (SPEC §8). A deterministic implementation is injected in tests so the
/// jitter — and therefore the whole backoff — is reproducible (TESTING-STRATEGY §4).
/// </summary>
public interface IJitterSource
{
    /// <summary>
    /// Returns the fraction (in the closed range <c>[0, 1]</c>) of the jitter window to add to the
    /// deterministic half of a backoff delay. <c>0</c> means "no jitter added" (delay is half the
    /// exponential value); <c>1</c> means "full jitter" (delay equals the exponential value).
    /// </summary>
    double NextFraction();
}

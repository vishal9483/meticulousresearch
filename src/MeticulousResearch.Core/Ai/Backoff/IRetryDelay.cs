namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// The single seam every backoff wait goes through so tests never sleep real time (SPEC §8,
/// TESTING-STRATEGY §4). Production waits on a real timer; a test double advances the injected
/// <see cref="Time.IClock"/> and records the requested durations without waiting.
/// </summary>
public interface IRetryDelay
{
    /// <summary>Waits for <paramref name="duration"/>, or until <paramref name="cancellationToken"/> fires.</summary>
    /// <param name="duration">How long to wait before the next retry attempt.</param>
    /// <param name="cancellationToken">Cancels the wait promptly.</param>
    Task Wait(TimeSpan duration, CancellationToken cancellationToken);
}

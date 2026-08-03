namespace MeticulousResearch.Core.Time;

/// <summary>
/// Abstraction over the system clock so timestamps, backoff jitter, and time-window
/// calculations are deterministic in tests (TESTING-STRATEGY.md §4).
/// </summary>
public interface IClock
{
    /// <summary>The current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

using MeticulousResearch.Core.Ai.Backoff;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// Deterministic <see cref="IRetryDelay"/> for tests: never sleeps real time. It records every
/// requested backoff duration and advances the injected <see cref="FakeClock"/> by that amount, so
/// tests can assert the honored wait (including <c>retry-after</c>) purely through the clock
/// (rate-limit-backoff, SPEC §8; TESTING-STRATEGY §4).
/// </summary>
public sealed class RecordingRetryDelay : IRetryDelay
{
    private readonly FakeClock _clock;

    /// <summary>Creates the delay over a <see cref="FakeClock"/> it advances instead of waiting.</summary>
    public RecordingRetryDelay(FakeClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>The backoff durations requested, in order.</summary>
    public List<TimeSpan> Delays { get; } = new();

    /// <inheritdoc />
    public Task Wait(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(duration);
        _clock.Advance(duration);
        return Task.CompletedTask;
    }
}

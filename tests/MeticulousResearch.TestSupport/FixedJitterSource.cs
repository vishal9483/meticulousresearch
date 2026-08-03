using MeticulousResearch.Core.Ai.Backoff;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// Deterministic <see cref="IJitterSource"/> for tests (TESTING-STRATEGY §4): returns a fixed jitter
/// fraction and records how many times it was consulted so a test can prove a jitter component was
/// applied to each backoff delay (rate-limit-backoff, SPEC §8).
/// </summary>
public sealed class FixedJitterSource : IJitterSource
{
    private readonly double _fraction;

    /// <summary>Creates a source that always returns <paramref name="fraction"/> (clamped to [0,1] by the policy).</summary>
    public FixedJitterSource(double fraction) => _fraction = fraction;

    /// <summary>The number of times <see cref="NextFraction"/> has been called.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public double NextFraction()
    {
        CallCount++;
        return _fraction;
    }
}

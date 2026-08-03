namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>
/// Production <see cref="IJitterSource"/> backed by a pseudo-random generator, yielding a uniformly
/// distributed jitter fraction in <c>[0, 1)</c> for each retry (SPEC §8 exponential backoff + jitter).
/// </summary>
public sealed class RandomJitterSource : IJitterSource
{
    private readonly Random _random;

    /// <summary>Creates a source over the shared thread-safe <see cref="Random"/>.</summary>
    public RandomJitterSource() : this(Random.Shared) { }

    /// <summary>Creates a source over an explicit (optionally seeded) <see cref="Random"/>.</summary>
    /// <param name="random">The generator to draw jitter fractions from.</param>
    public RandomJitterSource(Random random) =>
        _random = random ?? throw new ArgumentNullException(nameof(random));

    /// <inheritdoc />
    public double NextFraction() => _random.NextDouble();
}

using MeticulousResearch.Core.Time;

namespace MeticulousResearch.E2E.Support;

/// <summary>
/// A deterministic <see cref="IClock"/> that advances by a fixed step on every read, so persisted
/// timestamps strictly increase (stable turn/version ordering) and measured latencies are positive
/// without wall-clock flakiness. Mirrors the helper used by the per-feature Core tests.
/// </summary>
public sealed class AdvancingClock : IClock
{
    private DateTimeOffset _now;
    private readonly TimeSpan _step;

    /// <summary>Creates an advancing clock from a start instant and a per-read step.</summary>
    public AdvancingClock(DateTimeOffset start, TimeSpan step)
    {
        _now = start;
        _step = step;
    }

    /// <summary>Creates an advancing clock at a fixed default start with a 5 ms step.</summary>
    public AdvancingClock()
        : this(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5))
    {
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow
    {
        get
        {
            var value = _now;
            _now += _step;
            return value;
        }
    }
}

using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Tests.Turns;

/// <summary>
/// A deterministic <see cref="IClock"/> that advances by a fixed step on every read, so persisted
/// timestamps strictly increase (stable turn ordering) and a measured latency is positive without
/// wall-clock flakiness.
/// </summary>
internal sealed class AdvancingClock : IClock
{
    private DateTimeOffset _now;
    private readonly TimeSpan _step;

    public AdvancingClock(DateTimeOffset start, TimeSpan step)
    {
        _now = start;
        _step = step;
    }

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

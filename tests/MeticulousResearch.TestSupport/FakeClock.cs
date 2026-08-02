using MeticulousResearch.Core.Time;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// Deterministic <see cref="IClock"/> for tests. The current time is fixed and only advances
/// when the test explicitly calls <see cref="Advance"/> or sets <see cref="UtcNow"/>.
/// </summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now) => UtcNow = now;

    /// <summary>A stable default instant for tests that don't care about the exact value.</summary>
    public FakeClock() : this(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) { }

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}

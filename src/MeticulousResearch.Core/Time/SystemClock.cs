namespace MeticulousResearch.Core.Time;

/// <summary>Production <see cref="IClock"/> backed by the real system clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

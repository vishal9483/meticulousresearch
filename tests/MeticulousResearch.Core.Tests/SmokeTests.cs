using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests;

/// <summary>
/// Baseline smoke tests proving the Core + TestSupport wiring builds and runs green.
/// Feature agents replace/extend these; they are not part of any feature's spec.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void FakeClock_advances_deterministically()
    {
        var clock = new FakeClock();
        var start = clock.UtcNow;

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(start.AddMinutes(5), clock.UtcNow);
    }

    [Fact]
    public void FakeEnvironment_set_and_clear_round_trips()
    {
        var env = new FakeEnvironment().Set("ANTHROPIC_API_KEY", "sk-test");
        Assert.Equal("sk-test", env.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

        env.Clear("ANTHROPIC_API_KEY");
        Assert.Null(env.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
    }
}

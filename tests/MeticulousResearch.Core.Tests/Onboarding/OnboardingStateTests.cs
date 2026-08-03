using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Tests.Onboarding;

/// <summary>
/// Faithful <c>@unit</c> translation of the first-run-trigger completed-flag persistence from
/// docs/features/onboarding/tests.md (SPEC §3.8, §9.1(1)): a fresh install has no completed
/// onboarding, and once marked complete the flag survives a "restart" (a fresh state over the same
/// store) so onboarding never runs again. Runs in the headless gate (no excluded Category trait)
/// over a real <see cref="OnboardingState"/> and temp SQLite store.
/// </summary>
public sealed class OnboardingStateTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;

    public OnboardingStateTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-onboarding-state", Guid.NewGuid().ToString("N"));
        _store = new DataStore(new AdvancingClock(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(1)), _dataDir);
        _store.Initialize();
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // Scenario: Onboarding runs on first launch
    //   Given a fresh installation with no completed onboarding
    //   When the app launches
    //   Then onboarding is shown starting at the Welcome step
    [Fact]
    public void Fresh_install_has_no_completed_onboarding_and_starts_at_welcome()
    {
        var state = new OnboardingState(_store);

        Assert.False(state.IsCompleted);
        Assert.Equal(OnboardingStep.Welcome, state.CurrentStep);
    }

    // Scenario: Onboarding does not run again after completion
    //   Given onboarding has been completed
    //   When the app launches
    //   Then onboarding is not shown
    [Fact]
    public void Completed_flag_persists_across_restart()
    {
        new OnboardingState(_store).MarkCompleted();

        // A fresh state over the same store models the next launch.
        var reloaded = new OnboardingState(_store);

        Assert.True(reloaded.IsCompleted);
    }

    // Supports the re-run-from-Settings scenario: Reset clears the flag and returns to Welcome.
    [Fact]
    public void Reset_clears_the_completed_flag_and_returns_to_welcome()
    {
        var state = new OnboardingState(_store);
        state.MarkCompleted();
        state.CurrentStep = OnboardingStep.Defaults;

        state.Reset();

        Assert.False(state.IsCompleted);
        Assert.Equal(OnboardingStep.Welcome, state.CurrentStep);
        Assert.False(new OnboardingState(_store).IsCompleted);
    }
}

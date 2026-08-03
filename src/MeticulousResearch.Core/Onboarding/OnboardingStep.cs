namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// The ordered steps of the first-run onboarding wizard (SPEC §3.8): Welcome → API key →
/// Defaults → Sample project → Done. Owned by <c>onboarding</c>; drives the wizard stepper's
/// can-advance/back logic and the persisted "current step" of <see cref="IOnboardingState"/>.
/// </summary>
public enum OnboardingStep
{
    /// <summary>Welcome + privacy statement (local-first, where data lives).</summary>
    Welcome = 0,

    /// <summary>API key entry with a "Test key" check.</summary>
    ApiKey = 1,

    /// <summary>Defaults: model tier, theme, data directory.</summary>
    Defaults = 2,

    /// <summary>Optional populated sample project.</summary>
    SampleProject = 3,

    /// <summary>Finish — mark complete and land on the Projects home.</summary>
    Done = 4,
}

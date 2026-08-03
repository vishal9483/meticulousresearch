namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// The persisted first-run state (SPEC §3.8): a "completed" flag plus the current wizard step.
/// Drives whether onboarding runs at launch (it runs only while <see cref="IsCompleted"/> is
/// <c>false</c>) and supports re-run from Settings via <see cref="Reset"/>. The completed flag is
/// stored under a stable key so a clean machine reliably triggers onboarding (v1-acceptance §9.1(1)).
/// </summary>
public interface IOnboardingState
{
    /// <summary>True once onboarding has been finished or skipped; onboarding never runs again while true.</summary>
    bool IsCompleted { get; }

    /// <summary>The step the wizard is currently on (defaults to <see cref="OnboardingStep.Welcome"/>).</summary>
    OnboardingStep CurrentStep { get; set; }

    /// <summary>Marks onboarding complete and persists the flag so it does not run on the next launch.</summary>
    void MarkCompleted();

    /// <summary>
    /// Clears the completed flag and resets the current step to <see cref="OnboardingStep.Welcome"/>
    /// so the wizard runs again — the "Re-run onboarding" entry point in Settings.
    /// </summary>
    void Reset();
}

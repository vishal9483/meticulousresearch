namespace MeticulousResearch.App.Tests.Onboarding;

/// <summary>
/// <c>@manual</c> scenario from docs/features/onboarding/tests.md (SPEC §3.8, §9.1(1)). Branding is
/// a human visual judgement, so this is a skipped placeholder carrying the review checklist.
/// </summary>
public sealed class OnboardingBrandingManualTests
{
    // @manual
    // Scenario: Onboarding reads as branded, finished software
    //   Given the first-run onboarding flow
    //   Then each step is branded, styled, and free of placeholder or unstyled chrome
    //
    // Manual checklist (verify against the running app):
    //   [ ] Welcome step uses the design-system tokens (surface/typography), not default chrome.
    //   [ ] Each step (Welcome, API key, Defaults, Sample project, Done) is fully styled.
    //   [ ] Next/Back/Skip affordances are consistent and clearly labelled on every step.
    //   [ ] No placeholder text, TODOs, or unstyled controls appear anywhere in the flow.
    //   [ ] The privacy statement and data-location text are legible and on-brand.
    [Fact(Skip = "@manual — branded, finished-software look verified by a human against the checklist in this file.")]
    [Trait("Category", "manual")]
    public void Onboarding_reads_as_branded_finished_software()
    {
    }
}

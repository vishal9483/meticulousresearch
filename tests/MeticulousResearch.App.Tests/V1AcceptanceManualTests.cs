namespace MeticulousResearch.App.Tests;

/// <summary>
/// v1.0 acceptance criteria 1 and 10 from docs/features/v1-acceptance/tests.md (SPEC §9.1). These
/// gate "install on a clean machine" and "no crashes/placeholders/raw errors across the whole
/// journey" — they can only be verified by a human on a fresh Windows 11 VM from the actual signed
/// installer, so they are tagged <c>Category=manual</c> and skipped in the automated gate. The
/// checklist body is the executable definition of done, checked off with screenshots on the PR.
/// </summary>
public class V1AcceptanceManualTests
{
    // Scenario: 1 — Install via a signed installer and launch to branded onboarding (§9.1.1)
    //   Given the signed release installer
    //   When I install the app and launch it from the Start Menu
    //   Then Windows shows a verified publisher (no unknown-publisher warning)
    //   And the app opens to the branded first-run onboarding welcome (product name, app icon, navy palette)
    //   And the welcome step states the app is local-first and where data lives
    //   And no crash, placeholder, or default WPF chrome appears
    //
    // Manual checklist (fresh Windows 11 x64 VM, no prior install, no app data):
    //   [ ] Install from the signed release installer produced by the `installer` feature.
    //   [ ] Windows/SmartScreen shows a VERIFIED publisher — no unknown-publisher warning.
    //   [ ] Launch from the Start Menu; the app opens without a crash.
    //   [ ] First-run onboarding welcome shows the product name ("MeticulousResearch Desktop"),
    //       the app icon, and the navy palette (design-system tokens, not default WPF chrome).
    //   [ ] The welcome step states the app is local-first and states where data is stored.
    //   [ ] No placeholder screen or default/unstyled WPF chrome appears anywhere on first launch.
    //   [ ] Attach screenshots of the publisher prompt and the branded welcome to the PR.
    [Fact(Skip = "@manual — clean-VM install/branding from the signed installer, verified by a human with screenshots on the PR.")]
    [Trait("Category", "manual")]
    public void Criterion_1_Install_via_signed_installer_and_launch_to_branded_onboarding()
    {
    }

    // Scenario: 10 — Complete the whole workflow with no crashes, no placeholder screens, and no raw errors (§9.1.10)
    //   Given I have performed criteria 1–9 end to end on the clean machine
    //   Then the app never crashed
    //   And no screen showed unstyled/default WPF chrome or a placeholder
    //   And every error I encountered was a human-readable message with a recovery action — never a raw stack trace
    //   And every list/view I reached had a designed empty, loading, or populated state
    //
    // Manual checklist (same fresh Windows 11 VM, after criteria 1–9 end to end):
    //   [ ] Performed criteria 1–9 in order on the clean machine with the signed build.
    //   [ ] The app never crashed at any step.
    //   [ ] No screen showed unstyled/default WPF chrome or a placeholder ("TODO"/blank/lorem).
    //   [ ] Every error induced (missing key, offline, rate limit, extraction failure) surfaced as a
    //       human-readable message WITH a recovery action — never a raw stack trace or status code.
    //   [ ] Every list/view reached showed a designed empty, loading, or populated state.
    //   [ ] Attach screenshots of each induced-error message and of representative empty/loading states.
    [Fact(Skip = "@manual — clean-VM end-to-end no-crash/no-placeholder/no-raw-error pass, verified by a human with screenshots on the PR.")]
    [Trait("Category", "manual")]
    public void Criterion_10_Whole_workflow_with_no_crashes_no_placeholders_no_raw_errors()
    {
    }
}

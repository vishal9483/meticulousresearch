namespace MeticulousResearch.App.Tests;

/// <summary>
/// @manual checklist scenario from docs/features/about-screen/tests.md. A visual branding/styling
/// pass performed by a human during PR review (SPEC §3.7, §9.1(10)); tagged
/// <c>Category=manual</c> and skipped in the automated gate.
/// </summary>
public class AboutScreenManualTests
{
    // Scenario: The About screen is branded and styled
    //   Given the About screen
    //   Then it presents app icon, product name, and version in the app's styled design
    //   And shows no unstyled default WPF chrome
    //
    // Manual checklist:
    //   [ ] Open Settings and choose "About" — the About screen appears.
    //   [ ] The app icon, product name ("MeticulousResearch Desktop"), and version are all shown.
    //   [ ] Typography, spacing, and colors use the design-system tokens (styled, not default).
    //   [ ] No unstyled default WPF chrome is visible (buttons/borders match the design system).
    [Fact(Skip = "@manual — visual branding/styling checklist, verified by a human during PR review.")]
    [Trait("Category", "manual")]
    public void The_About_screen_is_branded_and_styled()
    {
    }
}

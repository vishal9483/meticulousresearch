using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.Core.Tests.Theming;

/// <summary>
/// @manual scenario from docs/features/design-system-theming/tests.md. Subjective/visual coherence
/// is verified by a human against the checklist below (TESTING-STRATEGY §2), so this test is
/// tagged manual and skipped in every automated run.
/// </summary>
public class ThemeCoherenceManualTests
{
    // Scenario: Both themes present a coherent navy-based identity
    //   Given the app in Light and in Dark theme
    //   Then surfaces, text, and accents form a coherent professional palette in each
    //   And no screen shows unstyled default WPF chrome
    //
    // Manual checklist (verify in the PR against screenshots):
    //  [ ] Light theme: navy-based, professional palette; surfaces/text/accents read as one system.
    //  [ ] Dark theme: same identity, comfortable contrast, no washed-out or clashing accents.
    //  [ ] No screen shows default WPF chrome (unstyled Button/TextBox/ComboBox/DataGrid/Dialog/Toast).
    //  [ ] Switching Light↔Dark live keeps layout stable (no restart, no flash of unstyled content).
    [Fact(Skip = "@manual — visual coherence verified by a human against the checklist in this file.")]
    [Trait("Category", "manual")]
    public void Both_themes_present_a_coherent_navy_based_identity()
    {
        // Manual verification only; kept as a placeholder so the scenario is traceable.
        _ = DesignTokens.For(AppTheme.Light);
        _ = DesignTokens.For(AppTheme.Dark);
    }
}

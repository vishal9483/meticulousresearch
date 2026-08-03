using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.Core.Tests.Accessibility;

/// <summary>
/// @unit scenarios from docs/features/accessibility/tests.md broadening design-system-theming's
/// contrast check to cover body text, primary button text, and the focus indicator in both themes
/// (SPEC §8, §3.7).
/// </summary>
public class ContrastGuaranteesTests
{
    // Scenario Outline: Text and interactive controls meet WCAG-AA contrast in each theme
    //   Given the "<theme>" theme tokens
    //   Then body text on its surface has a contrast ratio of at least 4.5:1
    //   And primary button text on its fill has a contrast ratio of at least 4.5:1
    //   And the focus indicator meets at least 3:1 against its surface
    //   | Light |
    //   | Dark  |
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void Text_and_interactive_controls_meet_wcag_aa_contrast(AppTheme theme)
    {
        var tokens = DesignTokens.For(theme);

        var bodyOnSurface = DesignTokens.ContrastRatio(tokens.OnSurface, tokens.Surface);
        Assert.True(bodyOnSurface >= 4.5,
            $"{theme} body text on surface was {bodyOnSurface:F2}:1, below the WCAG-AA 4.5:1 floor.");

        var buttonTextOnFill = DesignTokens.ContrastRatio(tokens.OnPrimary, tokens.PrimaryButtonFill);
        Assert.True(buttonTextOnFill >= 4.5,
            $"{theme} primary button text on fill was {buttonTextOnFill:F2}:1, below 4.5:1.");

        var focusOnSurface = DesignTokens.ContrastRatio(tokens.FocusIndicator, tokens.Surface);
        Assert.True(focusOnSurface >= 3.0,
            $"{theme} focus indicator on surface was {focusOnSurface:F2}:1, below the 3:1 floor.");
    }
}

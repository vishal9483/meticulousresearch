using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.Core.Tests.Theming;

/// <summary>
/// @unit scenarios covering the brand token palette and WCAG-AA contrast
/// (docs/features/design-system-theming/tests.md).
/// </summary>
public class DesignTokensTests
{
    // Scenario: The brand palette exposes the required design tokens
    //   Then it defines a primary navy color token
    //   And a single accent token
    //   And neutral surface tokens
    //   And semantic success, warning, and error tokens
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void The_brand_palette_exposes_the_required_design_tokens(AppTheme theme)
    {
        var tokens = DesignTokens.For(theme);

        // a primary navy color token — navy is a dark, blue-dominant color.
        Assert.True(tokens.PrimaryNavy.B > tokens.PrimaryNavy.R,
            "primary navy should be blue-dominant");
        Assert.True(tokens.PrimaryNavy.B > tokens.PrimaryNavy.G,
            "primary navy should be blue-dominant");

        // the two neutral surfaces are distinct roles.
        Assert.NotEqual(tokens.Surface, tokens.SurfaceVariant);

        // the three semantic colors are distinct from one another.
        Assert.NotEqual(tokens.Success, tokens.Warning);
        Assert.NotEqual(tokens.Warning, tokens.Error);
        Assert.NotEqual(tokens.Success, tokens.Error);
    }

    // Scenario Outline: Core text/background pairs meet WCAG-AA contrast
    //   Given the "<theme>" theme tokens
    //   Then the body text on primary surface contrast ratio is at least 4.5:1
    //   | Light |
    //   | Dark  |
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void Core_text_background_pairs_meet_wcag_aa_contrast(AppTheme theme)
    {
        var tokens = DesignTokens.For(theme);

        var ratio = DesignTokens.ContrastRatio(tokens.OnSurface, tokens.Surface);

        Assert.True(ratio >= 4.5,
            $"{theme} body-on-surface contrast was {ratio:F2}:1, below the WCAG-AA 4.5:1 floor.");
    }

    [Fact]
    public void ContrastRatio_of_black_on_white_is_maximal()
    {
        var ratio = DesignTokens.ContrastRatio(
            new TokenColor(0, 0, 0), new TokenColor(255, 255, 255));

        Assert.True(ratio > 20.9 && ratio <= 21.0, $"expected ~21:1, got {ratio:F2}");
    }

    [Fact]
    public void For_rejects_the_unresolved_System_theme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DesignTokens.For(AppTheme.System));
    }
}

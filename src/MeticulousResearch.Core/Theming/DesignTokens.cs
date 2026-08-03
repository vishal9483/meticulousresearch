namespace MeticulousResearch.Core.Theming;

/// <summary>
/// The canonical navy-based palette exposed to <c>@unit</c> tests and consumed by the WPF
/// resource dictionaries. This is the single source of truth for token values so palette
/// membership and WCAG-AA contrast are asserted without the UI (design-system-theming/phase.md).
/// </summary>
public static class DesignTokens
{
    private static readonly ThemeTokens LightTokens = new()
    {
        Theme = AppTheme.Light,
        PrimaryNavy = TokenColor.FromHex("#1B2A4A"),
        Accent = TokenColor.FromHex("#2E5AAC"),
        Surface = TokenColor.FromHex("#FFFFFF"),
        SurfaceVariant = TokenColor.FromHex("#F3F4F6"),
        OnSurface = TokenColor.FromHex("#1A1D21"),
        PrimaryButtonFill = TokenColor.FromHex("#1B2A4A"),
        OnPrimary = TokenColor.FromHex("#FFFFFF"),
        FocusIndicator = TokenColor.FromHex("#2E5AAC"),
        Success = TokenColor.FromHex("#1E7E34"),
        Warning = TokenColor.FromHex("#8A5A00"),
        Error = TokenColor.FromHex("#B3261E"),
    };

    private static readonly ThemeTokens DarkTokens = new()
    {
        Theme = AppTheme.Dark,
        PrimaryNavy = TokenColor.FromHex("#24365C"),
        Accent = TokenColor.FromHex("#7FA8E8"),
        Surface = TokenColor.FromHex("#12161C"),
        SurfaceVariant = TokenColor.FromHex("#1C222B"),
        OnSurface = TokenColor.FromHex("#E6E8EB"),
        PrimaryButtonFill = TokenColor.FromHex("#24365C"),
        OnPrimary = TokenColor.FromHex("#E6E8EB"),
        FocusIndicator = TokenColor.FromHex("#7FA8E8"),
        Success = TokenColor.FromHex("#5CD07A"),
        Warning = TokenColor.FromHex("#E0A94B"),
        Error = TokenColor.FromHex("#F2726A"),
    };

    /// <summary>
    /// Returns the token set for a <b>resolved</b> theme. <see cref="AppTheme.System"/> is not a
    /// resolved theme and is rejected — callers must resolve it first via the theme service.
    /// </summary>
    public static ThemeTokens For(AppTheme resolvedTheme) => resolvedTheme switch
    {
        AppTheme.Light => LightTokens,
        AppTheme.Dark => DarkTokens,
        _ => throw new ArgumentOutOfRangeException(nameof(resolvedTheme),
            "DesignTokens.For expects a resolved theme (Light or Dark), not System."),
    };

    /// <summary>
    /// The WCAG 2.x contrast ratio between two colors, in the range 1.0 … 21.0. Body text meeting
    /// AA requires ≥ 4.5:1.
    /// </summary>
    public static double ContrastRatio(TokenColor a, TokenColor b)
    {
        var la = a.RelativeLuminance();
        var lb = b.RelativeLuminance();
        var lighter = Math.Max(la, lb);
        var darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }
}

namespace MeticulousResearch.Core.Theming;

/// <summary>
/// The semantic design tokens for one resolved theme (Light or Dark). Views reference these
/// roles rather than raw colors so themes swap cleanly (design-system-theming/phase.md).
/// </summary>
public sealed class ThemeTokens
{
    /// <summary>The resolved theme these tokens belong to (Light or Dark).</summary>
    public required AppTheme Theme { get; init; }

    /// <summary>Primary corporate navy.</summary>
    public required TokenColor PrimaryNavy { get; init; }

    /// <summary>The single accent color.</summary>
    public required TokenColor Accent { get; init; }

    /// <summary>The primary surface (page/background) color.</summary>
    public required TokenColor Surface { get; init; }

    /// <summary>A secondary neutral surface (cards, raised panels).</summary>
    public required TokenColor SurfaceVariant { get; init; }

    /// <summary>Body text / foreground drawn on <see cref="Surface"/>.</summary>
    public required TokenColor OnSurface { get; init; }

    /// <summary>Semantic success color.</summary>
    public required TokenColor Success { get; init; }

    /// <summary>Semantic warning color.</summary>
    public required TokenColor Warning { get; init; }

    /// <summary>Semantic error color.</summary>
    public required TokenColor Error { get; init; }
}

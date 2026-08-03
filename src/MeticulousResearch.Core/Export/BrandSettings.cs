namespace MeticulousResearch.Core.Export;

/// <summary>
/// The brand inputs the export theme consumes (SPEC §3.7): an accent color (token or hex), a firm
/// logo path, and a confidentiality notice. Owned by <c>settings-secure-key</c> / Settings;
/// <c>branded-export</c> only reads them. When no accent is configured the theme falls back to a
/// professional corporate navy (<see cref="DefaultNavyAccent"/>).
/// </summary>
/// <param name="Accent">The configured accent token/hex, or null/empty to use the default navy.</param>
/// <param name="LogoPath">The firm logo path placed on the cover/header, or null when unset.</param>
/// <param name="Confidentiality">The confidentiality notice shown in headers/footers, or null.</param>
public sealed record BrandSettings(string? Accent, string? LogoPath, string? Confidentiality)
{
    /// <summary>The default professional corporate navy accent used when none is configured.</summary>
    public const string DefaultNavyAccent = "navy";

    /// <summary>Brand settings with nothing configured (theme uses the default navy palette).</summary>
    public static BrandSettings Unset { get; } = new(Accent: null, LogoPath: null, Confidentiality: null);

    /// <summary>
    /// The accent actually applied to the document: the configured <see cref="Accent"/> when set,
    /// otherwise the default corporate navy.
    /// </summary>
    public string ResolvedAccent =>
        string.IsNullOrWhiteSpace(Accent) ? DefaultNavyAccent : Accent!.Trim();

    /// <summary>Whether an accent was explicitly configured (vs. falling back to the default navy).</summary>
    public bool HasConfiguredAccent => !string.IsNullOrWhiteSpace(Accent);
}

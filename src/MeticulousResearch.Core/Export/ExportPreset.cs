namespace MeticulousResearch.Core.Export;

/// <summary>
/// The three chrome presets a branded export can apply (SPEC §3.4.2): <c>Client-ready report</c>
/// (full chrome — cover, TOC, running header/footer, sources/methodology), <c>Internal draft</c>
/// (minimal chrome), and <c>Plain</c> (content only).
/// </summary>
public enum ExportPreset
{
    /// <summary>Full chrome: cover page, auto TOC, running header/footer, sources section.</summary>
    ClientReady,

    /// <summary>Minimal chrome: no cover or TOC, a lightweight running footer only.</summary>
    InternalDraft,

    /// <summary>No chrome: content only.</summary>
    Plain,
}

/// <summary>Parsing helpers for <see cref="ExportPreset"/>.</summary>
public static class ExportPresets
{
    /// <summary>
    /// Parses a preset label (<c>Client-ready report | Internal draft | Plain</c>) into an
    /// <see cref="ExportPreset"/>.
    /// </summary>
    /// <param name="label">The preset label from the UI or a scenario.</param>
    /// <returns>The matching <see cref="ExportPreset"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is not a supported preset.</exception>
    public static ExportPreset Parse(string label) => (label ?? "").Trim().ToLowerInvariant() switch
    {
        "client-ready report" => ExportPreset.ClientReady,
        "internal draft" => ExportPreset.InternalDraft,
        "plain" => ExportPreset.Plain,
        _ => throw new ArgumentException($"'{label}' is not a supported export preset.", nameof(label)),
    };
}

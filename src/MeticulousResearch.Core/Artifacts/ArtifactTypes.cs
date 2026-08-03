namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// The registry of the five v1 artifact types and their default <c>content_format</c> (SPEC §3.4):
/// <c>doc→markdown</c>, <c>text→text</c>, <c>code→code</c>, <c>table→csv</c>,
/// <c>diagram→mermaid</c>. Owned by <c>artifact-creation</c> so every creation path validates the
/// type against a single source of truth; downstream M3 features (templates, versioning, diff,
/// edit-with-claude, report-composition) consume this mapping rather than redefining it.
/// </summary>
public static class ArtifactTypes
{
    /// <summary>Document artifact (Markdown).</summary>
    public const string Doc = "doc";

    /// <summary>Plain-text artifact.</summary>
    public const string Text = "text";

    /// <summary>Source-code artifact.</summary>
    public const string Code = "code";

    /// <summary>Tabular artifact (CSV).</summary>
    public const string Table = "table";

    /// <summary>Diagram artifact (Mermaid source).</summary>
    public const string Diagram = "diagram";

    private static readonly IReadOnlyDictionary<string, string> Formats =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Doc] = "markdown",
            [Text] = "text",
            [Code] = "code",
            [Table] = "csv",
            [Diagram] = "mermaid",
        };

    /// <summary>The five supported artifact types, in canonical order.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Doc, Text, Code, Table, Diagram };

    // Aliases the model's emit_artifact/Write tool vocabulary uses for the canonical types
    // (SPEC §7.4). Normalized at the emit boundary so the persisted type is always canonical.
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["document"] = Doc,
            ["markdown"] = Doc,
            ["md"] = Doc,
            ["plaintext"] = Text,
            ["csv"] = Table,
            ["mermaid"] = Diagram,
        };

    /// <summary>Whether <paramref name="type"/> is one of the five supported artifact types.</summary>
    public static bool IsKnown(string? type) => type is not null && Formats.ContainsKey(type);

    /// <summary>
    /// Maps a canonical type or a tool-vocabulary alias (e.g. <c>document→doc</c>,
    /// <c>csv→table</c>, <c>mermaid→diagram</c>) to its canonical type, or null when unrecognized.
    /// </summary>
    public static string? Normalize(string? type)
    {
        if (type is null)
            return null;
        if (Formats.ContainsKey(type))
            return type;
        return Aliases.TryGetValue(type, out var canonical) ? canonical : null;
    }

    /// <summary>
    /// The default <c>content_format</c> for <paramref name="type"/>
    /// (e.g. <c>doc→markdown</c>, <c>table→csv</c>, <c>diagram→mermaid</c>).
    /// </summary>
    /// <exception cref="ArtifactValidationException"><paramref name="type"/> is not a supported type.</exception>
    public static string FormatFor(string type)
    {
        if (type is not null && Formats.TryGetValue(type, out var format))
            return format;
        throw new ArtifactValidationException($"'{type}' is not a supported artifact type.");
    }
}

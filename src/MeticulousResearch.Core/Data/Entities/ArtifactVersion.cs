namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// One immutable revision of an <see cref="Artifact"/>, carrying its content and the provenance
/// (model, prompt, usage) of how it was produced. Maps to the <c>ArtifactVersion</c> table (SPEC §5).
/// </summary>
public sealed class ArtifactVersion
{
    /// <summary>Stable version identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Owning artifact id (FK to <see cref="Artifact"/>).</summary>
    public string ArtifactId { get; set; } = "";

    /// <summary>Monotonic per-artifact version number (1-based).</summary>
    public long VersionNo { get; set; }

    /// <summary>The version's content.</summary>
    public string Content { get; set; } = "";

    /// <summary>Content format (e.g. <c>markdown | html | code | mermaid</c>).</summary>
    public string? ContentFormat { get; set; }

    /// <summary>Model id used to generate this version (nullable for manual edits).</summary>
    public string? Model { get; set; }

    /// <summary>Prompt used to generate this version (nullable for manual edits).</summary>
    public string? Prompt { get; set; }

    /// <summary>Input tokens for a generated version (0 for manual edits).</summary>
    public long TokensIn { get; set; }

    /// <summary>Output tokens for a generated version (0 for manual edits).</summary>
    public long TokensOut { get; set; }

    /// <summary>Snapshot USD cost for a generated version (nullable/0 for manual edits).</summary>
    public double? CostUsd { get; set; }

    /// <summary>JSON array of resource ids that were in scope when generating (nullable).</summary>
    public string? ResourceScopeJson { get; set; }

    /// <summary>Who produced the version: <c>user | claude</c>.</summary>
    public string CreatedBy { get; set; } = "";

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";
}

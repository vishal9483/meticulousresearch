namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// A piece of source material attached to a project (pasted text, uploaded file, URL, or a
/// reference to an artifact). Maps to the <c>Resource</c> table (SPEC §5).
/// </summary>
public sealed class Resource
{
    /// <summary>Stable resource identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Owning project id (FK to <see cref="Project"/>).</summary>
    public string ProjectId { get; set; } = "";

    /// <summary>Display title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Resource kind: <c>text | file | url | artifact_ref</c>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Original path or URL the resource came from (nullable).</summary>
    public string? SourceUri { get; set; }

    /// <summary>Path to the stored original blob under the project files directory (nullable).</summary>
    public string? BlobPath { get; set; }

    /// <summary>Path to the extracted-text file used for search/context (nullable).</summary>
    public string? ExtractedPath { get; set; }

    /// <summary>
    /// The extracted body text, denormalized from <see cref="ExtractedPath"/>'s file into the
    /// database so the <c>ResourceFts</c> full-text index (SPEC §5) can index the searchable body
    /// (its triggers read this column). Kept in sync by the resource service on add/re-extract.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>Size of the original in bytes (nullable when not file-backed).</summary>
    public long? ByteSize { get; set; }

    /// <summary>Deterministic token estimate for context-budget planning (nullable).</summary>
    public long? TokenEstimate { get; set; }

    /// <summary>Whether the resource is included when building conversation context.</summary>
    public bool Enabled { get; set; }

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";

    /// <summary>UTC last-modified instant (ISO-8601).</summary>
    public string UpdatedAt { get; set; } = "";
}

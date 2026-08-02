namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// A generated deliverable (document/text/code/table/diagram) with a version history.
/// Maps to the <c>Artifact</c> table (SPEC §5).
/// </summary>
public sealed class Artifact
{
    /// <summary>Stable artifact identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Owning project id (FK to <see cref="Project"/>).</summary>
    public string ProjectId { get; set; } = "";

    /// <summary>Display title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Artifact kind: <c>doc | text | code | table | diagram</c>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Id of the current <see cref="ArtifactVersion"/> (nullable before first version).</summary>
    public string? CurrentVersionId { get; set; }

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";

    /// <summary>UTC last-modified instant (ISO-8601).</summary>
    public string UpdatedAt { get; set; } = "";
}

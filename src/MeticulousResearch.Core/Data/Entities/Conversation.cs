namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// A chat thread within a project. Maps to the <c>Conversation</c> table (SPEC §5).
/// </summary>
public sealed class Conversation
{
    /// <summary>Stable conversation identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Owning project id (FK to <see cref="Project"/>).</summary>
    public string ProjectId { get; set; } = "";

    /// <summary>Display title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Default model id for turns in this conversation (nullable).</summary>
    public string? ModelDefault { get; set; }

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";

    /// <summary>UTC last-modified instant (ISO-8601).</summary>
    public string UpdatedAt { get; set; } = "";
}

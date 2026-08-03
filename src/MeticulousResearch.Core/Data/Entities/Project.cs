namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// A research project — the top-level container for resources, conversations, and artifacts.
/// Maps to the <c>Project</c> table (SPEC §5).
/// </summary>
public sealed class Project
{
    /// <summary>Stable project identifier (opaque string, e.g. a GUID).</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional per-project system/custom instructions injected into conversations.</summary>
    public string? CustomInstructions { get; set; }

    /// <summary>Optional default model id for new conversations in this project.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Optional accent color (hex/token) for the project chrome.</summary>
    public string? Color { get; set; }

    /// <summary>Whether the project is archived (hidden from the active list).</summary>
    public bool Archived { get; set; }

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";

    /// <summary>UTC last-modified instant (ISO-8601).</summary>
    public string UpdatedAt { get; set; } = "";
}

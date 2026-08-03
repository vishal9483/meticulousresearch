using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Backup;

/// <summary>
/// The relational subset carried in a project backup (SPEC §8): exactly the target project's rows
/// across the six portable tables. Serialized as <c>data.json</c> in the archive. Token columns are
/// preserved verbatim so cost recomputes identically on restore; the credential vault and any other
/// project's rows are excluded by construction.
/// </summary>
public sealed class ProjectBackupData
{
    /// <summary>The single backed-up project row.</summary>
    public Project Project { get; set; } = new();

    /// <summary>The project's resource rows.</summary>
    public List<Resource> Resources { get; set; } = new();

    /// <summary>The project's conversation rows.</summary>
    public List<Conversation> Conversations { get; set; } = new();

    /// <summary>The messages belonging to the project's conversations.</summary>
    public List<Message> Messages { get; set; } = new();

    /// <summary>The project's artifact rows.</summary>
    public List<Artifact> Artifacts { get; set; } = new();

    /// <summary>The versions belonging to the project's artifacts.</summary>
    public List<ArtifactVersion> ArtifactVersions { get; set; } = new();
}

using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The artifact domain contract (SPEC §3.4, §5, §7.4): the structured <c>emit_artifact</c>/
/// <c>update_artifact</c> tool surface the Agent SDK loop calls, plus the four creation paths
/// (promote a turn, generate directly, generate from a template seam, create blank) and the basic
/// management operations. The emit/update surface is pinned by <c>ai-gateway</c>; the domain surface
/// is owned by <c>artifact-creation</c> (M3). Later M3 features extend this seam without reshaping
/// the model: <c>artifact-versioning</c> takes over version history, <c>deliverable-templates</c>
/// builds on <see cref="Generate"/>, and <c>edit-with-claude</c>/manual edit create versions via
/// <see cref="SetContent"/>. Every write routes through here — never a silent file overwrite (§7.4).
/// </summary>
public interface IArtifactService
{
    /// <summary>Creates a new artifact and its first version from the model's <c>emit_artifact</c> call.</summary>
    ArtifactMutationResult EmitArtifact(ArtifactEmitCommand command);

    /// <summary>Records a new version of an existing artifact from the model's <c>update_artifact</c> call.</summary>
    ArtifactMutationResult UpdateArtifact(ArtifactUpdateCommand command);

    /// <summary>
    /// Creation path 4 — creates a blank artifact of <paramref name="type"/> titled
    /// <paramref name="title"/> with an empty first version authored by the user (SPEC §3.4).
    /// </summary>
    /// <exception cref="ArtifactValidationException">The type is unknown or the title is empty.</exception>
    Artifact Create(string projectId, string type, string title);

    /// <summary>
    /// Creates an artifact and its version-1 record directly from <paramref name="content"/>, with
    /// the supplied <paramref name="provenance"/> (used by promote and generation). When
    /// <paramref name="contentFormat"/> is null the type's default format is used.
    /// </summary>
    /// <exception cref="ArtifactValidationException">The type is unknown or the title is empty.</exception>
    Artifact CreateFromContent(
        string projectId, string type, string title, string content, string? contentFormat,
        ArtifactProvenance provenance);

    /// <summary>
    /// Creation path 2 — generates an artifact directly from <paramref name="request"/> through
    /// <see cref="IChatService"/>, persisting the emitted content as version 1 and recording the
    /// prompt, model, in-scope resource ids, and usage as its provenance (SPEC §3.4).
    /// </summary>
    /// <exception cref="ArtifactValidationException">The prompt is empty or the type/title invalid.</exception>
    Task<Artifact> Generate(
        string projectId, GenerateArtifactRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creation path 1 — promotes the assistant turn <paramref name="turnId"/> into a <c>doc</c>
    /// artifact titled <paramref name="title"/>, copying the turn's content, model, in-scope
    /// resources, and usage onto version 1 (created_by <c>claude</c>) (SPEC §3.4).
    /// </summary>
    /// <exception cref="InvalidOperationException">The turn does not exist.</exception>
    Artifact PromoteTurn(string turnId, string title);

    /// <summary>
    /// Records a new user-authored version of an existing artifact from <paramref name="content"/>
    /// and makes it current. This is the minimal version-creation seam; <c>artifact-versioning</c>
    /// owns the full history semantics on top of it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The artifact does not exist.</exception>
    ArtifactVersion SetContent(string artifactId, string content);

    /// <summary>Returns the artifact with <paramref name="artifactId"/>, or null when none exists.</summary>
    Artifact? Get(string artifactId);

    /// <summary>Lists the artifacts in <paramref name="projectId"/>, most recently created first.</summary>
    IReadOnlyList<Artifact> List(string projectId);

    /// <summary>Renames an artifact to <paramref name="newTitle"/> and bumps its <c>updated_at</c>.</summary>
    /// <exception cref="ArtifactValidationException">The new title is empty.</exception>
    /// <exception cref="InvalidOperationException">The artifact does not exist.</exception>
    Artifact Rename(string artifactId, string newTitle);
}

/// <summary>A request to create a new artifact (<c>emit_artifact</c>).</summary>
/// <param name="ProjectId">The owning project.</param>
/// <param name="Title">The artifact title.</param>
/// <param name="Kind">The artifact kind (e.g. <c>document</c>, <c>table</c>, <c>code</c>, <c>diagram</c>).</param>
/// <param name="Content">The initial content.</param>
public sealed record ArtifactEmitCommand(string ProjectId, string Title, string Kind, string Content);

/// <summary>A request to record a new version of an existing artifact (<c>update_artifact</c>).</summary>
/// <param name="ArtifactId">The artifact to update.</param>
/// <param name="Content">The new content that becomes the next version.</param>
/// <param name="ChangeNote">An optional note describing the change.</param>
public sealed record ArtifactUpdateCommand(string ArtifactId, string Content, string? ChangeNote = null);

/// <summary>The outcome of an artifact emit/update: the artifact id and the resulting version number.</summary>
/// <param name="ArtifactId">The affected artifact id.</param>
/// <param name="Version">The newly-created version number (1 for a freshly emitted artifact).</param>
/// <param name="Title">The artifact title.</param>
public sealed record ArtifactMutationResult(string ArtifactId, int Version, string Title);

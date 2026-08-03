namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The structured artifact emit/update contract the Agent SDK loop calls to create or revise
/// artifacts (SPEC §7.4). Owned here so the gateway pins a stable surface, but exercised by later
/// features (<c>builtin-file-tools-sandbox</c>, M3 artifact features), which supply the real
/// versioning implementation. Kept minimal and stable.
/// </summary>
public interface IArtifactService
{
    /// <summary>Creates a new artifact and its first version from the model's <c>emit_artifact</c> call.</summary>
    ArtifactMutationResult EmitArtifact(ArtifactEmitCommand command);

    /// <summary>Records a new version of an existing artifact from the model's <c>update_artifact</c> call.</summary>
    ArtifactMutationResult UpdateArtifact(ArtifactUpdateCommand command);
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

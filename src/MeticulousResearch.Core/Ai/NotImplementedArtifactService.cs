namespace MeticulousResearch.Core.Ai;

/// <summary>
/// A loud seam for <see cref="IArtifactService"/> until the artifact versioning implementation is
/// delivered by <c>builtin-file-tools-sandbox</c> and the M3 artifact features. It never fakes a
/// pass: every call throws <see cref="NotSupportedException"/> naming the owning feature, so wiring
/// mistakes surface loudly rather than silently succeeding.
/// </summary>
public sealed class NotImplementedArtifactService : IArtifactService
{
    private const string Owner =
        "Artifact emit/update is implemented by builtin-file-tools-sandbox and the M3 artifact features.";

    /// <inheritdoc />
    public ArtifactMutationResult EmitArtifact(ArtifactEmitCommand command) =>
        throw new NotSupportedException(Owner);

    /// <inheritdoc />
    public ArtifactMutationResult UpdateArtifact(ArtifactUpdateCommand command) =>
        throw new NotSupportedException(Owner);
}

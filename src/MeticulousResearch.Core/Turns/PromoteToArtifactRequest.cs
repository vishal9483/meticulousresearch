namespace MeticulousResearch.Core.Turns;

/// <summary>
/// The provenance recorded on a promote-to-artifact request (SPEC §3.3): which assistant turn the
/// artifact was promoted from, the model that produced it, and the resource ids that were in scope.
/// Lets <c>artifact-creation</c> (M3) trace an artifact back to its originating turn.
/// </summary>
/// <param name="SourceTurnId">The id of the assistant <c>Message</c> the artifact was promoted from.</param>
/// <param name="Model">The model id that produced the source turn, or <c>null</c> when unrecorded.</param>
/// <param name="ResourceScope">The resource ids that were in scope for the source turn.</param>
public sealed record TurnProvenance(
    string SourceTurnId,
    string? Model,
    IReadOnlyList<string> ResourceScope);

/// <summary>
/// A request to create an artifact from an assistant turn (SPEC §3.3). Built by
/// <c>turn-metadata-actions</c> and consumed by <c>artifact-creation</c> (M3): it carries the turn's
/// <see cref="Content"/> and its <see cref="Provenance"/>. This feature only assembles the request;
/// the artifact domain (storage/versioning) is owned downstream.
/// </summary>
/// <param name="Content">The assistant turn's text, which becomes the artifact's content.</param>
/// <param name="Provenance">The source-turn provenance recorded on the artifact.</param>
public sealed record PromoteToArtifactRequest(string Content, TurnProvenance Provenance);

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// Raised when a create/generate/rename request is invalid — an unknown artifact type, an empty
/// prompt, or a missing title — so no artifact is persisted (SPEC §3.4). Distinct from
/// <see cref="ArtifactContractException"/>, which flags a malformed structured tool call.
/// </summary>
public sealed class ArtifactValidationException : Exception
{
    /// <summary>Creates the exception with a human-readable validation message.</summary>
    public ArtifactValidationException(string message) : base(message) { }
}

/// <summary>
/// Raised when the model's structured <c>emit_artifact</c>/<c>update_artifact</c> tool call violates
/// the §7.4 contract (a missing required field). The call is rejected and no write occurs, so a
/// malformed tool call can never silently create or overwrite an artifact.
/// </summary>
public sealed class ArtifactContractException : Exception
{
    /// <summary>Creates the exception with a human-readable contract-violation message.</summary>
    public ArtifactContractException(string message) : base(message) { }
}

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// The provenance stamped onto every <c>ArtifactVersion</c> (SPEC §5): who produced it, and — for a
/// generated version — the model, prompt, in-scope resource ids, and token usage. Carried onto each
/// version so an artifact can always be traced back to how it was produced. Blank/manual versions
/// use <see cref="User"/> (no model/prompt, zero usage); promoted turns and generated artifacts use
/// <see cref="Claude"/>. Owned by <c>artifact-creation</c>; consumed by <c>cost-tracking</c> (M4)
/// which reads the persisted token figures.
/// </summary>
public sealed record ArtifactProvenance
{
    /// <summary><c>created_by</c> value for a user/manual version.</summary>
    public const string CreatedByUser = "user";

    /// <summary><c>created_by</c> value for a model-produced version.</summary>
    public const string CreatedByClaude = "claude";

    /// <summary>Who produced the version: <see cref="CreatedByUser"/> or <see cref="CreatedByClaude"/>.</summary>
    public required string CreatedBy { get; init; }

    /// <summary>The model that produced the version (null for user/manual versions).</summary>
    public string? Model { get; init; }

    /// <summary>The prompt used to generate the version (null when not generated from a prompt).</summary>
    public string? Prompt { get; init; }

    /// <summary>The resource ids that were in scope when generating (empty when none).</summary>
    public IReadOnlyList<string> ResourceScope { get; init; } = Array.Empty<string>();

    /// <summary>Billed input tokens for a generated version (0 for manual versions).</summary>
    public long TokensIn { get; init; }

    /// <summary>Billed output tokens for a generated version (0 for manual versions).</summary>
    public long TokensOut { get; init; }

    /// <summary>Snapshot USD cost for a generated version (null until known).</summary>
    public double? CostUsd { get; init; }

    /// <summary>Provenance for a user-authored (blank/manual) version: no model/prompt, zero usage.</summary>
    public static ArtifactProvenance User() => new() { CreatedBy = CreatedByUser };

    /// <summary>Provenance for a model-produced version, carrying model/prompt/scope/usage.</summary>
    public static ArtifactProvenance Claude(
        string? model,
        string? prompt,
        IReadOnlyList<string> resourceScope,
        long tokensIn,
        long tokensOut,
        double? costUsd = null) => new()
        {
            CreatedBy = CreatedByClaude,
            Model = model,
            Prompt = prompt,
            ResourceScope = resourceScope ?? Array.Empty<string>(),
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            CostUsd = costUsd,
        };
}

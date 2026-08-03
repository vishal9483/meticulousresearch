namespace MeticulousResearch.Core.Templates;

/// <summary>
/// One research deliverable template (SPEC §3.4.1): a config-driven, structured prompt plus a
/// section scaffold and a target artifact type that steer Claude to firm-quality, grounded output.
/// Templates are the recommended path for reports (SPEC §3.4 path 3) and are surfaced in the
/// New-artifact and New-project flows. Owned by <c>deliverable-templates</c>; loaded from a config
/// JSON (a shipped default merged with a Settings override, mirroring the model catalog §6.3) so the
/// firm can add house formats without a rebuild.
/// </summary>
public sealed record DeliverableTemplate
{
    /// <summary>Stable template identifier (e.g. <c>market-research-report</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name (e.g. <c>Market Research Report</c>).</summary>
    public required string Name { get; init; }

    /// <summary>A one-line description shown in the gallery preview.</summary>
    public required string Description { get; init; }

    /// <summary>The artifact type this template produces (an <c>ArtifactTypes</c> value, e.g. <c>doc</c>/<c>table</c>).</summary>
    public required string TargetType { get; init; }

    /// <summary>Ordered section headings that scaffold the deliverable and drive the gallery preview.</summary>
    public required IReadOnlyList<string> SectionScaffold { get; init; }

    /// <summary>
    /// The generation prompt, carrying the <c>{scope}</c>, <c>{horizon}</c>, and <c>{region}</c>
    /// placeholders the assembler substitutes before generation.
    /// </summary>
    public required string GenerationPrompt { get; init; }

    /// <summary>The recommended default model tier (e.g. <c>Deep</c>, <c>Balanced</c>).</summary>
    public required string DefaultModelTier { get; init; }
}

namespace MeticulousResearch.Core.Models;

/// <summary>
/// The model-catalog contract (SPEC §6, §6.3) owned by <c>model-selector</c>. Backed by the
/// config-driven catalog JSON (a shipped default, overridable in Settings), it exposes the friendly
/// tiers, the "All models" (additional) list, the default model, tier/id resolution, vision
/// capability, and per-id prices. Downstream features consume it read-only: <c>ai-gateway</c>/
/// <c>streaming</c> send the resolved model id, <c>cost-tracking</c> reads prices by id, and
/// <c>image-attachments</c> checks vision capability. Loading is deterministic and falls back to the
/// shipped default (with a warning) on malformed input.
/// </summary>
public interface IModelCatalog
{
    /// <summary>The friendly tiers in catalog order (<c>Frontier</c>, <c>Deep</c>, <c>Balanced</c>, <c>Fast</c>).</summary>
    IReadOnlyList<ModelInfo> Tiers { get; }

    /// <summary>The additional (non-tier) models shown under the "All models" list (§6.2).</summary>
    IReadOnlyList<ModelInfo> AdditionalModels { get; }

    /// <summary>The default project model id for new conversations (§6, e.g. <c>claude-opus-5</c>).</summary>
    string DefaultModelId { get; }

    /// <summary>
    /// Resolves a tier name (e.g. <c>Balanced</c>) or a concrete model id to its <see cref="ModelInfo"/>.
    /// Tier matching is case-insensitive; a raw id is passed through when present in the catalog.
    /// Returns <c>null</c> when neither a tier nor an id matches.
    /// </summary>
    /// <param name="tierOrId">A friendly tier name or a concrete model id.</param>
    ModelInfo? Resolve(string tierOrId);

    /// <summary>Returns the model with the given id, or <c>null</c> when it is not in the catalog.</summary>
    ModelInfo? TryGet(string id);

    /// <summary>
    /// Whether the model with the given id accepts image (vision) input (§3.2.1). Returns
    /// <c>false</c> for an unknown id.
    /// </summary>
    bool IsVisionCapable(string id);

    /// <summary>
    /// Returns the per-MTok prices for the model with the given id (§6.3), or <c>null</c> when the
    /// id is unknown. <c>cost-tracking</c> uses this to compute a turn's cost from persisted tokens.
    /// </summary>
    ModelPrice? GetPrice(string id);
}

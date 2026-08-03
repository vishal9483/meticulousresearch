namespace MeticulousResearch.Core.Models;

/// <summary>
/// One selectable model in the catalog (SPEC §6.1–§6.3). A tier model carries a friendly
/// <see cref="Tier"/> name (e.g. <c>Frontier</c>); an "All models" (additional) model has a
/// <c>null</c> <see cref="Tier"/>. All metadata (context window, output cap, per-MTok prices, and
/// the vision flag) is sourced from the config-driven catalog JSON owned by <c>model-selector</c>.
/// </summary>
public sealed record ModelInfo
{
    /// <summary>The friendly tier name (<c>Frontier</c>/<c>Deep</c>/<c>Balanced</c>/<c>Fast</c>), or <c>null</c> for a non-tier model.</summary>
    public string? Tier { get; init; }

    /// <summary>The human-readable model name (e.g. <c>Claude Opus 5</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The concrete Claude API model id (e.g. <c>claude-opus-5</c>) sent to the backend.</summary>
    public required string Id { get; init; }

    /// <summary>The model's context window in tokens.</summary>
    public int ContextTokens { get; init; }

    /// <summary>The model's maximum output tokens per turn.</summary>
    public int MaxOutputTokens { get; init; }

    /// <summary>Input price in USD per million tokens (drives cost tracking, §3.6).</summary>
    public double PriceInputMTok { get; init; }

    /// <summary>Output price in USD per million tokens (drives cost tracking, §3.6).</summary>
    public double PriceOutputMTok { get; init; }

    /// <summary>Whether the model accepts image (vision) input (§3.2.1).</summary>
    public bool Vision { get; init; }

    /// <summary>Whether this model is exposed as a friendly tier (has a <see cref="Tier"/>).</summary>
    public bool IsTier => !string.IsNullOrEmpty(Tier);
}

/// <summary>
/// The per-MTok prices for a model, in USD (SPEC §6.3). <c>cost-tracking</c> multiplies these by the
/// persisted token counts to compute a turn's cost, so this feature exposes them by model id.
/// </summary>
/// <param name="InputMTok">Input price in USD per million tokens.</param>
/// <param name="OutputMTok">Output price in USD per million tokens.</param>
public readonly record struct ModelPrice(double InputMTok, double OutputMTok);

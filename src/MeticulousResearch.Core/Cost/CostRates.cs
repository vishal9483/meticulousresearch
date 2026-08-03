namespace MeticulousResearch.Core.Cost;

/// <summary>
/// The per-million-token (MTok) USD prices for a single model, owned by <c>cost-tracking</c> (SPEC
/// §3.6, §6.3). Unlike <see cref="MeticulousResearch.Core.Models.ModelPrice"/> (input/output only),
/// this carries the prompt-cache read and write rates so a turn's four billed token classes can be
/// priced at their own rates. Prices are sourced from the model catalog (owned by
/// <c>model-selector</c>); a model absent from the catalog has no <see cref="CostRates"/>.
/// </summary>
/// <param name="InputMTok">Input (prompt) price in USD per million tokens.</param>
/// <param name="OutputMTok">Output (completion) price in USD per million tokens.</param>
/// <param name="CacheReadMTok">Prompt-cache read price in USD per million tokens.</param>
/// <param name="CacheWriteMTok">Prompt-cache write price in USD per million tokens.</param>
public readonly record struct CostRates(
    decimal InputMTok,
    decimal OutputMTok,
    decimal CacheReadMTok,
    decimal CacheWriteMTok);

namespace MeticulousResearch.Core.Resources;

/// <summary>
/// A simple deterministic <see cref="ITokenEstimator"/> approximating ~4 characters per token
/// (SPEC §3.2). Any non-empty text yields a positive estimate. Estimates are purely local and
/// offline — there is no network or model-API dependency — and the same input always maps to the
/// same output. These estimates are for pre-send planning only and are surfaced labeled
/// "estimated"; authoritative token counts come from API usage fields later (SPEC §3.6).
/// </summary>
/// <remarks>
/// <para><b>Documented tolerance.</b> On ordinary English prose this heuristic is designed to land
/// within <see cref="DocumentedTolerance"/> (±25%) of a real model tokenizer's count. It is a
/// planning aid, not an exact tokenizer.</para>
/// <para><b>Image tokens.</b> Images are estimated from pixel area at roughly one token per
/// <see cref="PixelsPerImageToken"/> pixels, mirroring vision-model sizing.</para>
/// </remarks>
public sealed class HeuristicTokenEstimator : ITokenEstimator
{
    private const int CharsPerToken = 4;

    /// <summary>Approximate pixels represented by a single image token (vision sizing heuristic).</summary>
    private const int PixelsPerImageToken = 750;

    /// <summary>
    /// The documented accuracy target of the text heuristic relative to a real model tokenizer on
    /// ordinary prose, expressed as a fraction (0.25 = ±25%).
    /// </summary>
    public const double DocumentedTolerance = 0.25;

    /// <inheritdoc />
    public long Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Math.Max(1, (long)Math.Ceiling(text.Length / (double)CharsPerToken));
    }

    /// <inheritdoc />
    public long EstimateImageTokens(int widthPixels, int heightPixels)
    {
        if (widthPixels <= 0 || heightPixels <= 0)
            return 0;

        var area = (long)widthPixels * heightPixels;
        return Math.Max(1, area / PixelsPerImageToken);
    }
}

namespace MeticulousResearch.Core.Resources;

/// <summary>
/// A simple deterministic <see cref="ITokenEstimator"/> approximating ~4 characters per token
/// (SPEC §3.2). Any non-empty text yields a positive estimate. The <c>token-estimation</c> feature
/// replaces this with a refined estimator; the same input always maps to the same output.
/// </summary>
public sealed class HeuristicTokenEstimator : ITokenEstimator
{
    private const int CharsPerToken = 4;

    /// <inheritdoc />
    public long Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Math.Max(1, (long)Math.Ceiling(text.Length / (double)CharsPerToken));
    }
}

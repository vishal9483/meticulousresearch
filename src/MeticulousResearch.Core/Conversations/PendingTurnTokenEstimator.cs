using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// The pre-send token estimate for a pending turn (image-attachments, SPEC §3.2.1, §3.6): the
/// estimated text tokens plus an estimated image-token contribution for each attachment. The numbers
/// are purely local planning aids and are surfaced under the <see cref="Label"/> "estimated";
/// authoritative counts come from backend usage after the turn completes.
/// </summary>
/// <param name="TextTokens">Estimated tokens for the composed text.</param>
/// <param name="ImageTokens">Estimated tokens contributed by the attached images.</param>
public readonly record struct PendingTurnEstimate(long TextTokens, long ImageTokens)
{
    /// <summary>The combined estimated token total (text + images).</summary>
    public long TotalTokens => TextTokens + ImageTokens;

    /// <summary>The label these numbers are shown under (§3.6 provenance rule).</summary>
    public string Label => "estimated";
}

/// <summary>
/// Computes a <see cref="PendingTurnEstimate"/> for a pending turn's text plus its image
/// attachments, using the injected <see cref="ITokenEstimator"/> (whose image estimate is derived
/// from pixel dimensions). Pure and window-free so it is <c>@unit</c>-testable.
/// </summary>
public sealed class PendingTurnTokenEstimator
{
    private readonly ITokenEstimator _estimator;

    /// <summary>Creates the estimator over the injected token estimator.</summary>
    public PendingTurnTokenEstimator(ITokenEstimator estimator)
        => _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));

    /// <summary>
    /// Estimates the pre-send tokens of a turn composed of <paramref name="text"/> and
    /// <paramref name="attachments"/>. Each attachment contributes an estimated image-token amount
    /// derived from its pixel dimensions.
    /// </summary>
    public PendingTurnEstimate Estimate(string? text, IEnumerable<ImageAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);

        var textTokens = _estimator.Estimate(text ?? string.Empty);
        long imageTokens = 0;
        foreach (var attachment in attachments)
            imageTokens += _estimator.EstimateImageTokens(attachment.WidthPixels, attachment.HeightPixels);

        return new PendingTurnEstimate(textTokens, imageTokens);
    }
}

using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.E2E.Support;

/// <summary>
/// A deterministic, offline <see cref="IImageCaptioner"/> for the journeys: it never calls a model or
/// the network, returning a stable caption so image-vision resource flows (§3.2.1) are reproducible.
/// </summary>
public sealed class DeterministicImageCaptioner : IImageCaptioner
{
    /// <inheritdoc />
    public string Caption(string imagePath) =>
        $"Chart image showing an upward trend ({Path.GetFileName(imagePath)}).";
}

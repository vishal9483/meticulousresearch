namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// Configuration for image caption caching (SPEC §3.2.1). When <see cref="CaptionOnAdd"/> is enabled,
/// the resource service makes one small vision call through the injected <see cref="IImageCaptioner"/>
/// as an image is added and stores the caption as the resource's extracted text. Disabled by default
/// so the M1 add/store/preview path is network-free.
/// </summary>
public sealed class ImageCaptionOptions
{
    /// <summary>Whether to generate a caption via one small vision call as an image is added.</summary>
    public bool CaptionOnAdd { get; init; }

    /// <summary>The default: caption-on-add disabled (no network on add).</summary>
    public static ImageCaptionOptions Default { get; } = new();
}

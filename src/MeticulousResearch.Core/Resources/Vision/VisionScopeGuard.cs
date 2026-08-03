using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// The outcome of checking whether an in-scope image can be sent to the selected model (SPEC §3.2.1,
/// §6.3). When an enabled image is in scope but the selected model cannot accept image input, the app
/// warns and offers to switch to a vision-capable model — it never silently drops the image.
/// </summary>
/// <param name="HasImageInScope">Whether at least one enabled image resource is in scope.</param>
/// <param name="ModelAcceptsImages">Whether the selected model accepts image input (its <c>vision</c> flag).</param>
public sealed record VisionScopeDecision(bool HasImageInScope, bool ModelAcceptsImages)
{
    /// <summary>
    /// Whether the user should be warned: an image is in scope but the selected model is not
    /// vision-capable.
    /// </summary>
    public bool ShouldWarn => HasImageInScope && !ModelAcceptsImages;

    /// <summary>Whether a "switch to a vision-capable model" action should be offered (same condition).</summary>
    public bool OffersModelSwitch => ShouldWarn;

    /// <summary>
    /// Whether the image was silently dropped from the request. Always <c>false</c>: the app never
    /// drops an in-scope image — it warns and offers a switch instead (SPEC §3.2.1).
    /// </summary>
    public bool ImageSilentlyDropped => false;
}

/// <summary>
/// Guards a generation against a model that cannot accept in-scope images (SPEC §3.2.1 / §6.3). Owned
/// by <c>image-vision-caption</c> as a pure, window-free helper so it is <c>@unit</c>-testable; the
/// model's <c>vision</c> flag is supplied by the caller (from the config-driven model catalog once
/// <c>model-selector</c> lands in M2).
/// </summary>
public static class VisionScopeGuard
{
    /// <summary>
    /// Evaluates whether the selected model can accept the enabled image resources in scope.
    /// </summary>
    /// <param name="enabledResources">The resources currently in generation scope.</param>
    /// <param name="modelAcceptsImages">Whether the selected model's <c>vision</c> flag is set.</param>
    public static VisionScopeDecision Evaluate(
        IEnumerable<Resource> enabledResources, bool modelAcceptsImages)
    {
        ArgumentNullException.ThrowIfNull(enabledResources);
        var hasImage = enabledResources.Any(r => r.Type == ResourceTypes.Image);
        return new VisionScopeDecision(hasImage, modelAcceptsImages);
    }
}

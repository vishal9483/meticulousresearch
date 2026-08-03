namespace MeticulousResearch.Core.Models;

/// <summary>
/// An advisory (non-blocking) warning that the selected model cannot read images while an image is in
/// scope for the turn (SPEC §3.2.1). Carries a human-readable message and a suggested vision-capable
/// model id the picker offers to switch to. Produced by <see cref="ModelVisionAdvisor"/>.
/// </summary>
/// <param name="Message">The human-readable warning shown to the user.</param>
/// <param name="SuggestedVisionModelId">A vision-capable model id to switch to, or <c>null</c> when none exists.</param>
public sealed record VisionWarning(string Message, string? SuggestedVisionModelId);

/// <summary>
/// Advises whether selecting a model warrants a vision warning (SPEC §3.2.1): when an image is in
/// scope and the chosen model has <c>vision=false</c>, it produces an advisory
/// <see cref="VisionWarning"/> and suggests a vision-capable alternative. The warning is advisory and
/// never hard-blocks selection.
/// </summary>
public static class ModelVisionAdvisor
{
    /// <summary>
    /// Returns a <see cref="VisionWarning"/> when <paramref name="imageInScope"/> is true and the model
    /// with <paramref name="selectedModelId"/> is not vision-capable; otherwise <c>null</c>.
    /// </summary>
    /// <param name="catalog">The model catalog.</param>
    /// <param name="selectedModelId">The model id being selected.</param>
    /// <param name="imageInScope">Whether an image is attached to the turn.</param>
    public static VisionWarning? Advise(IModelCatalog catalog, string selectedModelId, bool imageInScope)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!imageInScope)
            return null;
        if (catalog.IsVisionCapable(selectedModelId))
            return null;

        var suggestion = catalog.Tiers.Concat(catalog.AdditionalModels)
            .FirstOrDefault(m => m.Vision)?.Id;

        var model = catalog.TryGet(selectedModelId);
        var name = model?.Name ?? selectedModelId;
        return new VisionWarning(
            $"{name} cannot read images. Switch to a vision-capable model to include the attached image.",
            suggestion);
    }
}

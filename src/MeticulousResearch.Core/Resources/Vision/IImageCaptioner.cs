namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// Produces a short text caption for an image via a single small vision call (SPEC §3.2.1). Owned by
/// <c>image-vision-caption</c> as an injectable seam over the real generation backend: the actual
/// Claude vision call is implemented by <c>ai-gateway</c> (M2) once its <c>IChatService</c> lands.
/// Until then the production wiring is a loud seam (see <see cref="NotImplementedImageCaptioner"/>),
/// while <c>@unit</c> tests inject a scripted fake so the add/store/caption behavior is deterministic
/// and offline.
/// </summary>
public interface IImageCaptioner
{
    /// <summary>
    /// Generates a short caption/description for the image stored at <paramref name="imagePath"/>.
    /// A failure surfaces as an exception; callers treat caption generation as non-fatal.
    /// </summary>
    /// <param name="imagePath">Absolute path to the stored original image.</param>
    /// <returns>A short human-readable caption.</returns>
    string Caption(string imagePath);
}

/// <summary>
/// The default production <see cref="IImageCaptioner"/> — a loud seam. The real vision call is owned
/// by <c>ai-gateway</c> (M2, <c>IChatService</c>); until it is wired, invoking this throws so no
/// fabricated network call ever ships. Caption-on-add is disabled by default, so this is never
/// reached on the M1 add/store/preview path.
/// </summary>
public sealed class NotImplementedImageCaptioner : IImageCaptioner
{
    /// <inheritdoc />
    public string Caption(string imagePath) =>
        throw new NotSupportedException(
            "Image caption generation is implemented by ai-gateway (M2) via IChatService; " +
            "inject an IImageCaptioner to enable caption-on-add.");
}

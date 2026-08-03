namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// The supported image formats (SPEC §3.2.1): PNG, JPG/JPEG, GIF, WEBP. Maps a file extension to its
/// MIME media type and reads pixel dimensions from image headers only — this is <em>not</em> an
/// OCR/vision library and never decodes or interprets image content (that is deferred to the model's
/// native vision at request time).
/// </summary>
public static class ImageFormats
{
    /// <summary>The supported, leading-dot-less, lowercase image extensions.</summary>
    private static readonly IReadOnlyDictionary<string, string> MediaTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["png"] = "image/png",
            ["jpg"] = "image/jpeg",
            ["jpeg"] = "image/jpeg",
            ["gif"] = "image/gif",
            ["webp"] = "image/webp",
        };

    /// <summary>The supported, leading-dot-less, lowercase image extensions (for file pickers).</summary>
    public static IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { "png", "jpg", "jpeg", "gif", "webp" };

    /// <summary>Whether <paramref name="extension"/> (with or without a leading dot) is supported.</summary>
    public static bool IsSupported(string extension) =>
        MediaTypesByExtension.ContainsKey(Normalize(extension));

    /// <summary>Normalizes a file path or extension to a leading-dot-less, lowercase extension.</summary>
    public static string NormalizeExtension(string filePathOrExtension)
    {
        var ext = Path.GetExtension(filePathOrExtension);
        if (string.IsNullOrEmpty(ext))
            ext = filePathOrExtension;
        return Normalize(ext);
    }

    /// <summary>
    /// Returns the MIME media type for a supported extension (e.g. <c>image/png</c>).
    /// </summary>
    /// <exception cref="UnsupportedImageTypeException">The extension is not a supported image type.</exception>
    public static string MediaTypeFor(string extension)
    {
        var normalized = Normalize(extension);
        if (!MediaTypesByExtension.TryGetValue(normalized, out var mediaType))
            throw new UnsupportedImageTypeException(normalized);
        return mediaType;
    }

    private static string Normalize(string value) =>
        (value ?? "").TrimStart('.').Trim().ToLowerInvariant();
}

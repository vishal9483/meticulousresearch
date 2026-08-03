using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// A per-turn image attachment pasted or attached directly into a conversation message (SPEC
/// §3.2.1). Unlike an image <em>resource</em> (<c>image-vision-caption</c>, M1), an attachment is
/// scoped to a single message: it is stored as message content, never registered as a project
/// <c>Resource</c>. The raw bytes are retained so a re-opened turn still shows the thumbnail and a
/// retry can re-send it. At request time it is emitted as the same vision block shape used by image
/// resources (<see cref="ImageContentBlock"/>).
/// </summary>
/// <param name="Id">Stable attachment identifier (used as the vision block's reference id).</param>
/// <param name="FileName">The attachment's file name (e.g. <c>chart.png</c>) — provenance only.</param>
/// <param name="MediaType">The MIME media type declared to the model (e.g. <c>image/png</c>).</param>
/// <param name="Bytes">The raw image bytes.</param>
/// <param name="WidthPixels">Image width in pixels (0 when the header could not be read).</param>
/// <param name="HeightPixels">Image height in pixels (0 when the header could not be read).</param>
public sealed record ImageAttachment(
    string Id,
    string FileName,
    string MediaType,
    byte[] Bytes,
    int WidthPixels,
    int HeightPixels)
{
    /// <summary>
    /// Builds an attachment from in-memory bytes (e.g. a pasted image), deriving the media type from
    /// the supplied <paramref name="fileName"/>'s extension and the pixel dimensions from the image
    /// header.
    /// </summary>
    /// <param name="fileName">The attachment file name (its extension selects the media type).</param>
    /// <param name="bytes">The raw image bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is blank or <paramref name="bytes"/> is empty.</exception>
    /// <exception cref="UnsupportedImageTypeException">The extension is not a supported image type.</exception>
    public static ImageAttachment FromBytes(string fileName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A file name is required.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
            throw new ArgumentException("Image bytes must be non-empty.", nameof(bytes));

        var mediaType = ImageFormats.MediaTypeFor(ImageFormats.NormalizeExtension(fileName));
        var dimensions = ImageHeaderReader.TryReadDimensions(bytes);
        return new ImageAttachment(
            Guid.NewGuid().ToString("N"),
            fileName,
            mediaType,
            bytes,
            dimensions?.Width ?? 0,
            dimensions?.Height ?? 0);
    }

    /// <summary>
    /// Builds an attachment from an image file on disk, reading its bytes, media type, and pixel
    /// dimensions.
    /// </summary>
    /// <param name="path">Absolute path to a supported image file.</param>
    /// <exception cref="UnsupportedImageTypeException">The file's extension is not a supported image type.</exception>
    public static ImageAttachment FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A file path is required.", nameof(path));
        var bytes = File.ReadAllBytes(path);
        return FromBytes(Path.GetFileName(path), bytes);
    }

    /// <summary>
    /// Emits this attachment as an <see cref="ImageContentBlock"/> — the same vision-block shape used
    /// by image resources and the built-in Read tool — with the bytes inlined as base64 at call time.
    /// </summary>
    public ImageContentBlock ToContentBlock()
        => new(Id, FileName, MediaType, Convert.ToBase64String(Bytes));
}

using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// Assembles the vision content block for an image resource at request time (SPEC §3.2.1). Kept
/// separate from persistence so M2 conversations/image-attachments and the built-in Read tool can
/// reuse it: it reads the stored original from disk and inlines its bytes as base64 <em>now</em>,
/// rather than persisting the bytes inline in the database.
/// </summary>
public sealed class VisionContentAssembler
{
    /// <summary>
    /// Reads the resource's stored original and produces an <see cref="ImageContentBlock"/> with the
    /// bytes inlined as base64 at call time. The block references the stored original for provenance.
    /// </summary>
    /// <param name="resource">An image resource with a stored original blob.</param>
    /// <returns>The assembled image content block.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The resource is not an image or has no stored original.</exception>
    public ImageContentBlock Assemble(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Type != ResourceTypes.Image)
            throw new InvalidOperationException(
                $"Resource '{resource.Id}' is not an image resource; cannot assemble a vision block.");

        if (string.IsNullOrEmpty(resource.BlobPath) || !File.Exists(resource.BlobPath))
            throw new InvalidOperationException(
                $"Image resource '{resource.Id}' has no stored original to assemble a vision block from.");

        var bytes = File.ReadAllBytes(resource.BlobPath);
        var mediaType = ImageFormats.MediaTypeFor(ImageFormats.NormalizeExtension(resource.BlobPath));
        var base64 = Convert.ToBase64String(bytes);

        return new ImageContentBlock(resource.Id, resource.BlobPath, mediaType, base64);
    }
}

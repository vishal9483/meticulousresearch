namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// An image content block assembled at request time for an image resource (SPEC §3.2.1). The raw
/// image bytes are read from the stored original and inlined as base64 <em>at assembly time</em> —
/// they are never persisted inline in the database. Reused by M2 conversations/image-attachments and
/// the built-in Read tool so every consumer sends images in the same shape.
/// </summary>
/// <param name="ResourceId">The image resource this block was assembled from.</param>
/// <param name="SourcePath">The stored original the bytes were read from (provenance/reference).</param>
/// <param name="MediaType">The MIME media type (e.g. <c>image/png</c>) declared to the model.</param>
/// <param name="Base64Data">The image bytes inlined as base64, assembled at request time.</param>
public sealed record ImageContentBlock(
    string ResourceId,
    string SourcePath,
    string MediaType,
    string Base64Data);

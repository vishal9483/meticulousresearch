namespace MeticulousResearch.TestSupport;

/// <summary>
/// A tiny, valid in-memory PNG (2×2) for tests that need real image bytes with a readable header —
/// e.g. image-attachments' per-turn attachment and token-estimate scenarios. It carries a correct
/// PNG signature and IHDR so <c>ImageHeaderReader</c> yields positive pixel dimensions; it is not a
/// renderable image and is used only to exercise byte/header handling deterministically offline.
/// </summary>
public static class SamplePng
{
    /// <summary>The declared width of <see cref="Bytes"/> in pixels.</summary>
    public const int Width = 2;

    /// <summary>The declared height of <see cref="Bytes"/> in pixels.</summary>
    public const int Height = 2;

    /// <summary>A 2×2 PNG with a valid signature and IHDR header.</summary>
    public static byte[] Bytes { get; } =
    {
        // PNG signature (8 bytes)
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        // IHDR chunk length (13)
        0x00, 0x00, 0x00, 0x0D,
        // "IHDR"
        0x49, 0x48, 0x44, 0x52,
        // width = 2
        0x00, 0x00, 0x00, 0x02,
        // height = 2
        0x00, 0x00, 0x00, 0x02,
        // bit depth, colour type, compression, filter, interlace
        0x08, 0x06, 0x00, 0x00, 0x00,
        // (partial) CRC — not validated by the header reader
        0x00, 0x00, 0x00, 0x00,
    };
}

using System.Buffers.Binary;

namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// Reads pixel dimensions from the <em>header bytes</em> of the supported image formats
/// (PNG/JPEG/GIF/WEBP). This is a tiny structural header parser — it never decodes pixels, runs OCR,
/// or interprets image content (SPEC §3.2.1: no OCR/vision library). Dimensions feed the image
/// token estimate only; the raw image itself is what the model reads via native vision at request
/// time. Unreadable/unknown headers yield <c>null</c> so callers can fall back gracefully.
/// </summary>
public static class ImageHeaderReader
{
    /// <summary>
    /// Attempts to read the pixel dimensions of the image at <paramref name="path"/> from its header.
    /// </summary>
    /// <returns>The (width, height) in pixels, or <c>null</c> when the header cannot be read.</returns>
    public static (int Width, int Height)? TryReadDimensions(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }

        return TryReadDimensions(bytes);
    }

    /// <summary>Attempts to read pixel dimensions from raw image <paramref name="bytes"/>.</summary>
    public static (int Width, int Height)? TryReadDimensions(ReadOnlySpan<byte> bytes)
    {
        if (IsPng(bytes))
            return ReadPng(bytes);
        if (IsGif(bytes))
            return ReadGif(bytes);
        if (IsWebp(bytes))
            return ReadWebp(bytes);
        if (IsJpeg(bytes))
            return ReadJpeg(bytes);
        return null;
    }

    private static bool IsPng(ReadOnlySpan<byte> b) =>
        b.Length >= 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
        && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    private static (int, int)? ReadPng(ReadOnlySpan<byte> b)
    {
        // IHDR width/height are big-endian 4-byte ints at offsets 16 and 20.
        var width = BinaryPrimitives.ReadInt32BigEndian(b.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(b.Slice(20, 4));
        return (width, height);
    }

    private static bool IsGif(ReadOnlySpan<byte> b) =>
        b.Length >= 10 && b[0] == (byte)'G' && b[1] == (byte)'I' && b[2] == (byte)'F';

    private static (int, int)? ReadGif(ReadOnlySpan<byte> b)
    {
        // Logical screen width/height are little-endian 2-byte values at offsets 6 and 8.
        var width = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(6, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(8, 2));
        return (width, height);
    }

    private static bool IsWebp(ReadOnlySpan<byte> b) =>
        b.Length >= 16 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
        && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P';

    private static (int, int)? ReadWebp(ReadOnlySpan<byte> b)
    {
        var fourCc = b.Slice(12, 4);
        // VP8X (extended): 24-bit little-endian (width-1) and (height-1) at offsets 24 and 27.
        if (fourCc[3] == (byte)'X' && b.Length >= 30)
        {
            var width = 1 + Read24LittleEndian(b.Slice(24, 3));
            var height = 1 + Read24LittleEndian(b.Slice(27, 3));
            return (width, height);
        }
        // VP8L (lossless): 14-bit width/height packed after the 0x2F signature at offset 21.
        if (fourCc[3] == (byte)'L' && b.Length >= 25 && b[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(21, 4));
            var width = (int)(bits & 0x3FFF) + 1;
            var height = (int)((bits >> 14) & 0x3FFF) + 1;
            return (width, height);
        }
        // VP8 (lossy): 14-bit width/height at offset 26/28 after the start code.
        if (fourCc[3] == (byte)' ' && b.Length >= 30)
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(26, 2)) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(28, 2)) & 0x3FFF;
            return (width, height);
        }
        return null;
    }

    private static int Read24LittleEndian(ReadOnlySpan<byte> b) => b[0] | (b[1] << 8) | (b[2] << 16);

    private static bool IsJpeg(ReadOnlySpan<byte> b) =>
        b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8;

    private static (int, int)? ReadJpeg(ReadOnlySpan<byte> b)
    {
        // Walk the marker segments to the Start-Of-Frame (SOFn) that carries height/width.
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = b[i + 1];

            // Standalone markers (no length): padding 0xFF, SOI/EOI, RSTn.
            if (marker == 0xFF || marker == 0xD8 || marker == 0xD9
                || (marker >= 0xD0 && marker <= 0xD7))
            {
                i += 2;
                continue;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 2, 2));

            // SOF0..SOF15 (excluding DHT 0xC4, DAC 0xCC, RSTn) carry frame dimensions.
            var isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isSof)
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 7, 2));
                return (width, height);
            }

            i += 2 + segmentLength;
        }

        return null;
    }
}

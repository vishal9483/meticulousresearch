namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Builds small, real fixture images (PNG/JPEG/GIF/WEBP) with valid structural headers and known
/// pixel dimensions, so the image-vision tests exercise the genuine add/store/dimension path with no
/// network and no OCR library. Bytes are the format's header (enough for storage and header-based
/// dimension reading); they are never decoded for pixel content.
/// </summary>
internal static class ImageFixtures
{
    /// <summary>Writes a supported image of the given extension with the given dimensions.</summary>
    public static string Write(string dir, string name, string ext, int width = 4, int height = 3)
    {
        var normalized = ext.TrimStart('.').ToLowerInvariant();
        var bytes = normalized switch
        {
            "png" => Png(width, height),
            "jpg" or "jpeg" => Jpeg(width, height),
            "gif" => Gif(width, height),
            "webp" => Webp(width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(ext), ext, "Unsupported fixture image type."),
        };
        return WriteRawBytes(dir, name, normalized, bytes);
    }

    /// <summary>Writes arbitrary bytes to <c>{dir}/{name}.{ext}</c> and returns the path.</summary>
    public static string WriteRawBytes(string dir, string name, string ext, byte[] bytes)
    {
        var path = Path.Combine(dir, $"{name}.{ext.TrimStart('.')}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Png(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // signature
        // IHDR chunk (length 13)
        b.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0D });
        b.AddRange("IHDR"u8.ToArray());
        b.AddRange(Be32(width));
        b.AddRange(Be32(height));
        b.AddRange(new byte[] { 0x08, 0x02, 0x00, 0x00, 0x00 }); // bit depth, colour type, comp, filter, interlace
        b.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // CRC placeholder (not validated by the reader)
        // IEND chunk
        b.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        b.AddRange("IEND"u8.ToArray());
        b.AddRange(new byte[] { 0xAE, 0x42, 0x60, 0x82 });
        return b.ToArray();
    }

    private static byte[] Gif(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange("GIF89a"u8.ToArray());
        b.AddRange(Le16(width));
        b.AddRange(Le16(height));
        b.AddRange(new byte[] { 0x00, 0x00, 0x00 }); // packed fields, bg colour, aspect
        b.Add(0x3B); // trailer
        return b.ToArray();
    }

    private static byte[] Jpeg(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange(new byte[] { 0xFF, 0xD8 }); // SOI
        // SOF0 marker with a length of 17 (2 len + 1 precision + 2 height + 2 width + 1 ncomp + 9 comp)
        b.AddRange(new byte[] { 0xFF, 0xC0 });
        b.AddRange(new byte[] { 0x00, 0x11 });
        b.Add(0x08); // precision
        b.AddRange(Be16(height));
        b.AddRange(Be16(width));
        b.Add(0x03); // number of components
        b.AddRange(new byte[] { 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 }); // component data
        b.AddRange(new byte[] { 0xFF, 0xD9 }); // EOI
        return b.ToArray();
    }

    private static byte[] Webp(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange("RIFF"u8.ToArray());
        b.AddRange(Le32(26)); // file size (arbitrary; not validated by the reader)
        b.AddRange("WEBP"u8.ToArray());
        b.AddRange("VP8X"u8.ToArray());
        b.AddRange(Le32(10)); // VP8X chunk size
        b.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // flags + reserved
        b.AddRange(Le24(width - 1));
        b.AddRange(Le24(height - 1));
        return b.ToArray();
    }

    private static byte[] Be32(int value) => new[]
    {
        (byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF),
    };

    private static byte[] Be16(int value) => new[] { (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };

    private static byte[] Le16(int value) => new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) };

    private static byte[] Le24(int value) => new[]
    {
        (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF),
    };

    private static byte[] Le32(int value) => new[]
    {
        (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF),
        (byte)((value >> 16) & 0xFF), (byte)((value >> 24) & 0xFF),
    };
}

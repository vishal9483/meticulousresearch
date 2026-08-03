using System.Globalization;

namespace MeticulousResearch.Core.Theming;

/// <summary>
/// An sRGB design-token color, exposed to <c>@unit</c> tests so palette membership and WCAG
/// contrast can be asserted without the WPF UI (design-system-theming/phase.md).
/// </summary>
public readonly struct TokenColor : IEquatable<TokenColor>
{
    /// <summary>Red channel (0–255).</summary>
    public byte R { get; }

    /// <summary>Green channel (0–255).</summary>
    public byte G { get; }

    /// <summary>Blue channel (0–255).</summary>
    public byte B { get; }

    /// <summary>Creates a color from 8-bit sRGB channels.</summary>
    public TokenColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>Parses a <c>#RRGGBB</c> (or <c>RRGGBB</c>) hex string.</summary>
    public static TokenColor FromHex(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        var s = hex.TrimStart('#');
        if (s.Length != 6)
            throw new FormatException($"Expected a #RRGGBB color, got '{hex}'.");
        return new TokenColor(
            byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    /// <summary>The <c>#RRGGBB</c> representation of this color.</summary>
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>
    /// The WCAG 2.x relative luminance of this color (0.0 for black … 1.0 for white).
    /// </summary>
    public double RelativeLuminance()
    {
        static double Channel(byte c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
    }

    /// <inheritdoc />
    public bool Equals(TokenColor other) => R == other.R && G == other.G && B == other.B;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TokenColor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (R << 16) | (G << 8) | B;

    /// <inheritdoc />
    public override string ToString() => Hex;
}

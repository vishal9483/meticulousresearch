namespace MeticulousResearch.Core.Resources.Vision;

/// <summary>
/// Thrown when an image resource is added for a type that is not supported (SPEC §3.2.1 supports
/// PNG/JPG/JPEG/GIF/WEBP). No resource is created; the caller surfaces the message to the analyst.
/// </summary>
public sealed class UnsupportedImageTypeException : Exception
{
    /// <summary>Creates the exception for the given (leading-dot-less, lowercase) extension.</summary>
    public UnsupportedImageTypeException(string extension)
        : base($"The image type '.{extension}' is not supported. Supported types are PNG, JPG, JPEG, GIF, and WEBP.")
    {
        Extension = extension;
    }

    /// <summary>The rejected extension (without a leading dot).</summary>
    public string Extension { get; }
}

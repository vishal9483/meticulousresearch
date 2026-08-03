namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Thrown when an upload is attempted for a file whose type is not supported by the extraction
/// pipeline (SPEC §3.2). No resource is created; the caller surfaces the message to the analyst.
/// </summary>
public sealed class UnsupportedFileTypeException : Exception
{
    /// <summary>Creates the exception for the given (leading-dot-less, lowercase) extension.</summary>
    public UnsupportedFileTypeException(string extension)
        : base($"The file type '.{extension}' is not supported.")
    {
        Extension = extension;
    }

    /// <summary>The rejected extension (without a leading dot).</summary>
    public string Extension { get; }
}

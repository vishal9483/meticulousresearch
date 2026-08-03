namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// The outcome of extracting text from an uploaded file (SPEC §3.2, §3.7). Surfaced so
/// <c>resource-management</c> can decide whether to offer a re-extract recovery action.
/// </summary>
public enum ExtractionStatus
{
    /// <summary>Text was extracted successfully.</summary>
    Success,

    /// <summary>The file parsed but yielded no extractable text (e.g. a scanned/image-only PDF).</summary>
    Empty,

    /// <summary>The file could not be parsed; the original blob is retained for re-extraction.</summary>
    Failed,
}

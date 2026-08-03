using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources.Extraction;

namespace MeticulousResearch.Core.Resources;

/// <summary>
/// The result of uploading a file (SPEC §3.2, §3.7): the saved <see cref="Resource"/> (whose
/// original blob is always stored) plus the extraction outcome. A failed or empty extraction still
/// yields a resource; <see cref="CanReExtract"/> tells the UI to offer a recovery action.
/// </summary>
public sealed class FileExtractionResult
{
    /// <summary>Creates the result of an upload.</summary>
    public FileExtractionResult(Resource resource, ExtractionStatus status, string? failureReason, string? hint)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Status = status;
        FailureReason = failureReason;
        Hint = hint;
    }

    /// <summary>The saved resource (its original blob is stored regardless of extraction outcome).</summary>
    public Resource Resource { get; }

    /// <summary>Whether extraction succeeded, produced no text, or failed.</summary>
    public ExtractionStatus Status { get; }

    /// <summary>A human-readable reason when <see cref="Status"/> is <see cref="ExtractionStatus.Failed"/>.</summary>
    public string? FailureReason { get; }

    /// <summary>
    /// A hint shown when extraction produced no text (e.g. suggesting a scanned PDF be added as an
    /// image resource for vision). Null otherwise.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    /// Whether the analyst should be offered a "re-extract" recovery action — true when extraction
    /// failed (SPEC §3.7). <c>resource-management</c> wires this to re-run the same extractor.
    /// </summary>
    public bool CanReExtract => Status == ExtractionStatus.Failed;
}

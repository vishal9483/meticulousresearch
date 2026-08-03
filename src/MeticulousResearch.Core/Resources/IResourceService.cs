using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources.Extraction;

namespace MeticulousResearch.Core.Resources;

/// <summary>
/// The resource domain contract (SPEC §3.2), owned by the text-paste-resource feature and extended
/// by the file/URL/image/management features. This slice supports the simplest resource type —
/// pasted text — plus read access; siblings add <c>AddFile</c>/<c>AddUrl</c>/<c>AddImage</c> and
/// rename/re-extract/toggle/remove. All resources land their searchable content as an
/// <c>extracted.txt</c> file under the project files directory.
/// </summary>
public interface IResourceService
{
    /// <summary>
    /// Adds a pasted-text resource to the project: validates the text is non-empty, writes it as
    /// the resource's extracted text under
    /// <c>projects/{projectId}/resources/{resourceId}/extracted.txt</c>, records its UTF-8 byte
    /// size and a deterministic token estimate, and persists the row (enabled, timestamped).
    /// </summary>
    /// <param name="projectId">Owning project id.</param>
    /// <param name="title">
    /// Display title; when null/whitespace the first non-empty line of <paramref name="text"/> is
    /// used (trimmed and length-capped).
    /// </param>
    /// <param name="text">The pasted text. Must contain non-whitespace content.</param>
    /// <returns>The saved <see cref="Resource"/>.</returns>
    /// <exception cref="ArgumentException">The text is empty or whitespace only.</exception>
    Resource AddText(string projectId, string? title, string text);

    /// <summary>
    /// Uploads a file resource (SPEC §3.2): copies the original into the resource directory as
    /// <c>original.{ext}</c>, runs the extraction pipeline for the file's type, writes the extracted
    /// text to <c>extracted.txt</c>, records source name / byte size / token estimate, and persists
    /// the row (type <c>file</c>, enabled, timestamped). A file that parses to no text is stored with
    /// an <see cref="ExtractionStatus.Empty"/> status and a hint; a file that cannot be parsed is
    /// stored with its original blob and an <see cref="ExtractionStatus.Failed"/> status plus a
    /// re-extract recovery affordance — neither crashes.
    /// </summary>
    /// <param name="projectId">Owning project id.</param>
    /// <param name="filePath">Absolute path to the file being uploaded.</param>
    /// <returns>The saved resource plus its extraction outcome.</returns>
    /// <exception cref="UnsupportedFileTypeException">The file's type is not supported; no resource is created.</exception>
    FileExtractionResult AddFile(string projectId, string filePath);

    /// <summary>Returns the resource with the given id, or <c>null</c> if it does not exist.</summary>
    Resource? Get(string resourceId);

    /// <summary>Returns the project's resources, most recently added first.</summary>
    IReadOnlyList<Resource> List(string projectId);

    /// <summary>
    /// Reads the extracted text of a resource from disk (its <c>extracted.txt</c>), or an empty
    /// string when the resource has no extracted text on disk.
    /// </summary>
    string GetExtractedText(string resourceId);
}

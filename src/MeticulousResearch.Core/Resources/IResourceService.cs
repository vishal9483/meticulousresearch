using MeticulousResearch.Core.Data.Entities;

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

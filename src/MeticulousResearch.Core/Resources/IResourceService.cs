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

    /// <summary>
    /// Adds a URL resource (SPEC §3.2): validates the URL, fetches the page once at add-time,
    /// extracts its main readable content and converts it to markdown (stripping navigation/ad
    /// boilerplate), writes that markdown to <c>extracted.txt</c>, stores the raw fetched HTML as the
    /// original blob, retains the exact original URL in <c>source_uri</c>, defaults the title to the
    /// page title, and records byte size and token estimate. Because conversion happens at add-time,
    /// preview and grounding work offline afterward. A malformed URL, a fetch failure
    /// (connection/timeout/HTTP error), or a page with no readable content produces an actionable
    /// error and creates no resource (SPEC §3.7).
    /// </summary>
    /// <param name="projectId">Owning project id.</param>
    /// <param name="url">The absolute http/https URL to fetch and convert.</param>
    /// <returns>The saved <see cref="Resource"/> (type <c>url</c>, enabled, timestamped).</returns>
    /// <exception cref="ArgumentException">The URL is missing or malformed; no resource is created.</exception>
    /// <exception cref="Url.UrlResourceException">The page could not be fetched or had no readable content; no resource is created.</exception>
    Resource AddUrl(string projectId, string url);

    /// <summary>Returns the resource with the given id, or <c>null</c> if it does not exist.</summary>
    Resource? Get(string resourceId);

    /// <summary>Returns the project's resources, most recently added first.</summary>
    IReadOnlyList<Resource> List(string projectId);

    /// <summary>
    /// Returns the project's <em>enabled</em> resources, most recently added first — the single
    /// source of truth for what the generation-context assembler includes (SPEC §3.2). Disabled
    /// resources are excluded.
    /// </summary>
    IReadOnlyList<Resource> ListEnabled(string projectId);

    /// <summary>
    /// Reads the extracted text of a resource from disk (its <c>extracted.txt</c>), or an empty
    /// string when the resource has no extracted text on disk.
    /// </summary>
    string GetExtractedText(string resourceId);

    /// <summary>
    /// Renames a resource (SPEC §3.2): validates the new title is non-blank, updates the title and
    /// <c>updated_at</c>, and persists. A blank title is rejected and the title is left unchanged.
    /// </summary>
    /// <param name="resourceId">The resource to rename.</param>
    /// <param name="newTitle">The new display title; must contain non-whitespace content.</param>
    /// <returns>The updated <see cref="Resource"/>.</returns>
    /// <exception cref="ArgumentException">The new title is null, empty, or whitespace only.</exception>
    /// <exception cref="InvalidOperationException">No resource has the given id.</exception>
    Resource Rename(string resourceId, string newTitle);

    /// <summary>
    /// Enables or disables a resource (SPEC §3.2). The enabled flag is the single source of truth
    /// for whether the resource is included in the assembled generation context. Updates
    /// <c>updated_at</c> and persists.
    /// </summary>
    /// <param name="resourceId">The resource to toggle.</param>
    /// <param name="enabled">Whether the resource should be included in generation scope.</param>
    /// <returns>The updated <see cref="Resource"/>.</returns>
    /// <exception cref="InvalidOperationException">No resource has the given id.</exception>
    Resource SetEnabled(string resourceId, bool enabled);

    /// <summary>
    /// Re-runs the appropriate extractor against the resource's stored original (SPEC §3.2, §3.7):
    /// refreshes <c>extracted.txt</c>, recomputes the token estimate, updates <c>updated_at</c>, and
    /// persists. File resources re-run the file extractor against the stored blob; URL resources
    /// re-convert the stored HTML (offline, idempotent). A previously-failed extraction can recover
    /// to <see cref="ExtractionStatus.Success"/>. Re-extract is unavailable for pasted-text
    /// resources, whose text is authored inline rather than extracted.
    /// </summary>
    /// <param name="resourceId">The resource to re-extract.</param>
    /// <returns>The refreshed resource plus its new extraction outcome.</returns>
    /// <exception cref="InvalidOperationException">No resource has the given id, or it has no stored original.</exception>
    /// <exception cref="NotSupportedException">The resource is a pasted-text resource and cannot be re-extracted.</exception>
    FileExtractionResult ReExtract(string resourceId);

    /// <summary>
    /// Removes a resource (SPEC §3.2): deletes its database row (its full-text-search rows follow via
    /// triggers) and its on-disk directory
    /// <c>projects/{projectId}/resources/{resourceId}</c> (original blob and extracted text). A
    /// missing resource is a no-op.
    /// </summary>
    /// <param name="resourceId">The resource to remove.</param>
    void Remove(string resourceId);
}

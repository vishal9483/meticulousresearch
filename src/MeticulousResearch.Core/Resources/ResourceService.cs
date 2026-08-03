using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Resources.Url;

namespace MeticulousResearch.Core.Resources;

/// <summary>
/// <see cref="IResourceService"/> over the <see cref="DataStore"/> and per-project file layout
/// (SPEC §3.2, §5). Follows the same pattern as <c>ProjectService</c>: short-lived
/// <c>AppDbContext</c> instances, timestamps from the store's <c>IClock</c>, and extracted text
/// written through <see cref="IProjectFileStore"/>. Pasted text has no original blob and no source
/// URI — the text itself is the extracted content.
/// </summary>
public sealed class ResourceService : IResourceService
{
    /// <summary>The file name of a resource's extracted text under its resource directory (SPEC §5).</summary>
    public const string ExtractedTextFileName = "extracted.txt";

    /// <summary>Maximum length of a title defaulted from the first line of pasted text.</summary>
    private const int DefaultTitleMaxLength = 120;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly DataStore _store;
    private readonly ITokenEstimator _estimator;
    private readonly FileExtractionPipeline _pipeline;
    private readonly IUrlFetcher _urlFetcher;
    private readonly HtmlToMarkdownConverter _htmlConverter = new();

    /// <summary>Creates the service over a data store and a token estimator (default extractors).</summary>
    public ResourceService(DataStore store, ITokenEstimator estimator)
        : this(store, estimator, FileExtractionPipeline.CreateDefault(), HttpUrlFetcher.CreateDefault())
    {
    }

    /// <summary>Creates the service over a data store, token estimator, and extraction pipeline.</summary>
    public ResourceService(DataStore store, ITokenEstimator estimator, FileExtractionPipeline pipeline)
        : this(store, estimator, pipeline, HttpUrlFetcher.CreateDefault())
    {
    }

    /// <summary>Creates the service over a data store, token estimator, and URL fetcher.</summary>
    public ResourceService(DataStore store, ITokenEstimator estimator, IUrlFetcher urlFetcher)
        : this(store, estimator, FileExtractionPipeline.CreateDefault(), urlFetcher)
    {
    }

    /// <summary>Creates the service over a data store, token estimator, extraction pipeline, and URL fetcher.</summary>
    public ResourceService(
        DataStore store, ITokenEstimator estimator, FileExtractionPipeline pipeline, IUrlFetcher urlFetcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _urlFetcher = urlFetcher ?? throw new ArgumentNullException(nameof(urlFetcher));
    }

    /// <inheritdoc />
    public Resource AddText(string projectId, string? title, string text)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Pasted text must not be empty or whitespace only.", nameof(text));

        var id = NewId();
        var resourceDir = _store.FileStore.GetResourceDirectory(projectId, id);
        var extractedPath = Path.Combine(resourceDir, ExtractedTextFileName);
        File.WriteAllText(extractedPath, text, Utf8NoBom);

        var now = Now();
        var resource = new Resource
        {
            Id = id,
            ProjectId = projectId,
            Title = ResolveTitle(title, text),
            Type = ResourceTypes.Text,
            SourceUri = null,
            BlobPath = null,
            ExtractedPath = extractedPath,
            ExtractedText = text,
            ByteSize = Encoding.UTF8.GetByteCount(text),
            TokenEstimate = _estimator.Estimate(text),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        using var db = _store.CreateDbContext();
        db.Resources.Add(resource);
        db.SaveChanges();
        return resource;
    }

    /// <inheritdoc />
    public FileExtractionResult AddFile(string projectId, string filePath)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Uploaded file was not found.", filePath);

        var extension = FileExtractionPipeline.NormalizeExtension(filePath);

        // Reject unsupported types before creating anything so no resource is created.
        var extractor = _pipeline.Resolve(filePath);

        var id = NewId();
        var resourceDir = _store.FileStore.GetResourceDirectory(projectId, id);

        // Store the original as a copy under the resource directory (never referenced in place).
        var blobPath = Path.Combine(resourceDir, $"original.{extension}");
        File.Copy(filePath, blobPath, overwrite: true);
        var byteSize = new FileInfo(blobPath).Length;

        // Extraction degrades gracefully: failures/empties still yield a stored resource.
        string extractedText;
        var status = ExtractionStatus.Success;
        string? failureReason = null;
        string? hint = null;
        try
        {
            var content = extractor.Extract(blobPath);
            extractedText = content.Text ?? "";
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                status = ExtractionStatus.Empty;
                hint = content.EmptyHint;
            }
        }
        catch (Exception ex)
        {
            extractedText = "";
            status = ExtractionStatus.Failed;
            failureReason = ex.Message;
        }

        var extractedPath = Path.Combine(resourceDir, ExtractedTextFileName);
        File.WriteAllText(extractedPath, extractedText, Utf8NoBom);

        var now = Now();
        var resource = new Resource
        {
            Id = id,
            ProjectId = projectId,
            Title = Path.GetFileNameWithoutExtension(filePath),
            Type = ResourceTypes.File,
            SourceUri = Path.GetFileName(filePath),
            BlobPath = blobPath,
            ExtractedPath = extractedPath,
            ExtractedText = extractedText,
            ByteSize = byteSize,
            TokenEstimate = _estimator.Estimate(extractedText),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        using (var db = _store.CreateDbContext())
        {
            db.Resources.Add(resource);
            db.SaveChanges();
        }

        return new FileExtractionResult(resource, status, failureReason, hint);
    }

    /// <inheritdoc />
    public Resource AddUrl(string projectId, string url)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));

        var normalized = (url ?? "").Trim();
        if (!IsValidHttpUrl(normalized))
            throw new ArgumentException($"'{url}' is not a valid URL. Enter a full http(s) address.", nameof(url));

        var fetch = _urlFetcher.Fetch(normalized);
        switch (fetch.Outcome)
        {
            case UrlFetchOutcome.ConnectionError:
                throw new UrlResourceException($"Could not connect to {normalized}. Check the address and your network.");
            case UrlFetchOutcome.Timeout:
                throw new UrlResourceException($"The request to {normalized} timed out. Try again later.");
            case UrlFetchOutcome.HttpError:
                throw new UrlResourceException(
                    $"The page could not be fetched from {normalized} (HTTP {fetch.StatusCode}).");
        }

        var rawHtml = fetch.Body ?? "";
        var converted = _htmlConverter.Convert(rawHtml);
        var markdown = converted.Markdown;
        if (string.IsNullOrWhiteSpace(markdown))
            throw new UrlResourceException($"No readable content was found at {normalized}.");

        var id = NewId();
        var resourceDir = _store.FileStore.GetResourceDirectory(projectId, id);

        // Store the raw fetched HTML so a later re-extract can re-convert without re-fetching.
        var blobPath = Path.Combine(resourceDir, "original.html");
        File.WriteAllText(blobPath, rawHtml, Utf8NoBom);

        var extractedPath = Path.Combine(resourceDir, ExtractedTextFileName);
        File.WriteAllText(extractedPath, markdown, Utf8NoBom);

        var now = Now();
        var resource = new Resource
        {
            Id = id,
            ProjectId = projectId,
            Title = ResolveUrlTitle(converted.Title, normalized),
            Type = ResourceTypes.Url,
            SourceUri = normalized,
            BlobPath = blobPath,
            ExtractedPath = extractedPath,
            ExtractedText = markdown,
            ByteSize = Encoding.UTF8.GetByteCount(markdown),
            TokenEstimate = _estimator.Estimate(markdown),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        using (var db = _store.CreateDbContext())
        {
            db.Resources.Add(resource);
            db.SaveChanges();
        }

        return resource;
    }

    /// <inheritdoc />
    public Resource? Get(string resourceId)
    {
        using var db = _store.CreateDbContext();
        return db.Resources.AsNoTracking().FirstOrDefault(r => r.Id == resourceId);
    }

    /// <inheritdoc />
    public IReadOnlyList<Resource> List(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Resources.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .ToList()
            .OrderByDescending(r => r.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public string GetExtractedText(string resourceId)
    {
        var resource = Get(resourceId)
            ?? throw new InvalidOperationException($"Resource '{resourceId}' not found.");

        if (string.IsNullOrEmpty(resource.ExtractedPath) || !File.Exists(resource.ExtractedPath))
            return "";

        return File.ReadAllText(resource.ExtractedPath, Encoding.UTF8);
    }

    /// <inheritdoc />
    public IReadOnlyList<Resource> ListEnabled(string projectId) =>
        List(projectId).Where(r => r.Enabled).ToList();

    /// <inheritdoc />
    public Resource Rename(string resourceId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("A resource title must not be blank.", nameof(newTitle));

        using var db = _store.CreateDbContext();
        var resource = db.Resources.FirstOrDefault(r => r.Id == resourceId)
            ?? throw new InvalidOperationException($"Resource '{resourceId}' not found.");

        resource.Title = newTitle.Trim();
        resource.UpdatedAt = Now();
        db.SaveChanges();
        return resource;
    }

    /// <inheritdoc />
    public Resource SetEnabled(string resourceId, bool enabled)
    {
        using var db = _store.CreateDbContext();
        var resource = db.Resources.FirstOrDefault(r => r.Id == resourceId)
            ?? throw new InvalidOperationException($"Resource '{resourceId}' not found.");

        resource.Enabled = enabled;
        resource.UpdatedAt = Now();
        db.SaveChanges();
        return resource;
    }

    /// <inheritdoc />
    public FileExtractionResult ReExtract(string resourceId)
    {
        using var db = _store.CreateDbContext();
        var resource = db.Resources.FirstOrDefault(r => r.Id == resourceId)
            ?? throw new InvalidOperationException($"Resource '{resourceId}' not found.");

        if (resource.Type == ResourceTypes.Text)
            throw new NotSupportedException(
                "Re-extract is not available for pasted-text resources; their text is authored inline.");

        if (string.IsNullOrEmpty(resource.BlobPath) || !File.Exists(resource.BlobPath))
            throw new InvalidOperationException(
                $"Resource '{resourceId}' has no stored original to re-extract from.");

        string extractedText;
        var status = ExtractionStatus.Success;
        string? failureReason = null;
        string? hint = null;

        if (resource.Type == ResourceTypes.Url)
        {
            // Re-convert the stored HTML offline (idempotent) rather than re-fetching the network.
            var rawHtml = File.ReadAllText(resource.BlobPath, Encoding.UTF8);
            var converted = _htmlConverter.Convert(rawHtml);
            extractedText = converted.Markdown ?? "";
            if (string.IsNullOrWhiteSpace(extractedText))
                status = ExtractionStatus.Empty;
        }
        else
        {
            var extractor = _pipeline.Resolve(resource.BlobPath);
            try
            {
                var content = extractor.Extract(resource.BlobPath);
                extractedText = content.Text ?? "";
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    status = ExtractionStatus.Empty;
                    hint = content.EmptyHint;
                }
            }
            catch (Exception ex)
            {
                extractedText = "";
                status = ExtractionStatus.Failed;
                failureReason = ex.Message;
            }
        }

        var extractedPath = string.IsNullOrEmpty(resource.ExtractedPath)
            ? Path.Combine(
                _store.FileStore.GetResourceDirectory(resource.ProjectId, resource.Id), ExtractedTextFileName)
            : resource.ExtractedPath;
        File.WriteAllText(extractedPath, extractedText, Utf8NoBom);

        resource.ExtractedPath = extractedPath;
        resource.ExtractedText = extractedText;
        resource.TokenEstimate = _estimator.Estimate(extractedText);
        resource.UpdatedAt = Now();
        db.SaveChanges();

        return new FileExtractionResult(resource, status, failureReason, hint);
    }

    /// <inheritdoc />
    public void Remove(string resourceId)
    {
        using var db = _store.CreateDbContext();
        var resource = db.Resources.FirstOrDefault(r => r.Id == resourceId);
        if (resource is null)
            return;

        var resourceDir = Path.Combine(
            _store.FileStore.DataDirectory, "projects", resource.ProjectId, "resources", resource.Id);

        db.Resources.Remove(resource);
        db.SaveChanges();

        if (Directory.Exists(resourceDir))
            Directory.Delete(resourceDir, recursive: true);
    }

    private static string ResolveTitle(string? title, string text)
    {
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();

        var firstLine = text
            .Split('\n')
            .Select(line => line.Trim('\r', ' ', '\t'))
            .FirstOrDefault(line => line.Length > 0) ?? "";

        if (firstLine.Length > DefaultTitleMaxLength)
            firstLine = firstLine.Substring(0, DefaultTitleMaxLength).TrimEnd();

        return firstLine;
    }

    private string Now() => _store.Clock.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static bool IsValidHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string ResolveUrlTitle(string? pageTitle, string url)
    {
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            var title = pageTitle.Trim();
            if (title.Length > DefaultTitleMaxLength)
                title = title.Substring(0, DefaultTitleMaxLength).TrimEnd();
            return title;
        }

        return url;
    }
}

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;

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

    /// <summary>Creates the service over a data store and a token estimator.</summary>
    public ResourceService(DataStore store, ITokenEstimator estimator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
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
}

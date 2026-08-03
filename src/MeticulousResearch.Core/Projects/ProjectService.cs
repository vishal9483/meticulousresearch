using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.Core.Projects;

/// <summary>
/// <see cref="IProjectService"/> over the <see cref="DataStore"/> (SQLite + per-project file
/// layout). Reads/writes through short-lived <see cref="AppDbContext"/> instances so no state is
/// cached across calls; timestamps come from the store's <c>IClock</c>.
/// </summary>
public sealed class ProjectService : IProjectService
{
    private readonly DataStore _store;
    private readonly ISettingsService _settings;

    /// <summary>Creates the service over a data store and the app settings.</summary>
    public ProjectService(DataStore store, ISettingsService settings)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public Project Create(
        string name,
        string? description = null,
        string? customInstructions = null,
        string? defaultModel = null,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var now = Now();
        var project = new Project
        {
            Id = NewId(),
            Name = name.Trim(),
            Description = description,
            CustomInstructions = customInstructions,
            DefaultModel = string.IsNullOrWhiteSpace(defaultModel) ? _settings.DefaultModel : defaultModel,
            Color = color,
            Archived = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        using var db = _store.CreateDbContext();
        db.Projects.Add(project);
        db.SaveChanges();
        return project;
    }

    /// <inheritdoc />
    public Project Rename(string projectId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Project name is required.", nameof(newName));

        using var db = _store.CreateDbContext();
        var project = db.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found.");

        project.Name = newName.Trim();
        project.UpdatedAt = Now();
        db.SaveChanges();
        return project;
    }

    /// <inheritdoc />
    public Project Duplicate(string projectId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Project name is required.", nameof(newName));

        using var db = _store.CreateDbContext();
        var source = db.Projects.AsNoTracking().FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found.");

        var sourceResources = db.Resources.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .ToList();

        var now = Now();
        var copy = new Project
        {
            Id = NewId(),
            Name = newName.Trim(),
            Description = source.Description,
            CustomInstructions = source.CustomInstructions,
            DefaultModel = source.DefaultModel,
            Color = source.Color,
            Archived = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Projects.Add(copy);

        foreach (var src in sourceResources)
        {
            var newResourceId = NewId();
            var (blobPath, extractedPath) = CopyResourceFiles(
                source.Id, src.Id, copy.Id, newResourceId, src.BlobPath, src.ExtractedPath);

            db.Resources.Add(new Resource
            {
                Id = newResourceId,
                ProjectId = copy.Id,
                Title = src.Title,
                Type = src.Type,
                SourceUri = src.SourceUri,
                BlobPath = blobPath,
                ExtractedPath = extractedPath,
                ByteSize = src.ByteSize,
                TokenEstimate = src.TokenEstimate,
                Enabled = src.Enabled,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        db.SaveChanges();
        return copy;
    }

    /// <inheritdoc />
    public Project Archive(string projectId) => SetArchived(projectId, archived: true);

    /// <inheritdoc />
    public Project Unarchive(string projectId) => SetArchived(projectId, archived: false);

    private Project SetArchived(string projectId, bool archived)
    {
        using var db = _store.CreateDbContext();
        var project = db.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found.");

        project.Archived = archived;
        project.UpdatedAt = Now();
        db.SaveChanges();
        return project;
    }

    /// <inheritdoc />
    public void Delete(string projectId)
    {
        using (var db = _store.CreateDbContext())
        {
            var project = db.Projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null)
                return;

            // FK ON DELETE CASCADE (with PRAGMA foreign_keys=ON) removes the child rows.
            db.Projects.Remove(project);
            db.SaveChanges();
        }

        var dir = Path.Combine(_store.FileStore.DataDirectory, "projects", projectId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    /// <inheritdoc />
    public Project? Get(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Projects.AsNoTracking().FirstOrDefault(p => p.Id == projectId);
    }

    /// <inheritdoc />
    public IReadOnlyList<Project> List(bool includeArchived = false)
    {
        using var db = _store.CreateDbContext();
        var query = db.Projects.AsNoTracking().AsQueryable();
        if (!includeArchived)
            query = query.Where(p => !p.Archived);

        return query
            .ToList()
            .OrderByDescending(p => p.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<Project> Search(string query)
    {
        using var db = _store.CreateDbContext();
        var active = db.Projects.AsNoTracking().Where(p => !p.Archived).ToList();

        var term = (query ?? "").Trim();
        if (term.Length == 0)
            return active;

        return active
            .Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (p.Description is not null && p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc />
    public ProjectDashboard GetDashboard(string projectId)
    {
        using var db = _store.CreateDbContext();
        var project = db.Projects.AsNoTracking().FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found.");

        var resourceCount = db.Resources.Count(r => r.ProjectId == projectId);
        var conversationCount = db.Conversations.Count(c => c.ProjectId == projectId);
        var artifactCount = db.Artifacts.Count(a => a.ProjectId == projectId);

        // Most recent activity across the project and its children.
        var stamps = new List<string> { project.UpdatedAt, project.CreatedAt };
        stamps.AddRange(db.Resources.Where(r => r.ProjectId == projectId).Select(r => r.UpdatedAt));
        stamps.AddRange(db.Conversations.Where(c => c.ProjectId == projectId).Select(c => c.UpdatedAt));
        stamps.AddRange(db.Artifacts.Where(a => a.ProjectId == projectId).Select(a => a.UpdatedAt));

        DateTimeOffset? lastActivity = stamps
            .Select(TryParse)
            .Where(d => d is not null)
            .DefaultIfEmpty(null)
            .Max();

        return new ProjectDashboard(projectId, resourceCount, conversationCount, artifactCount, lastActivity);
    }

    /// <inheritdoc />
    public string BuildSystemPromptContext(string projectId)
    {
        var project = Get(projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found.");

        return project.CustomInstructions?.Trim() ?? "";
    }

    private (string? blobPath, string? extractedPath) CopyResourceFiles(
        string sourceProjectId,
        string sourceResourceId,
        string newProjectId,
        string newResourceId,
        string? sourceBlobPath,
        string? sourceExtractedPath)
    {
        var sourceDir = Path.Combine(
            _store.FileStore.DataDirectory, "projects", sourceProjectId, "resources", sourceResourceId);

        // Nothing on disk to copy.
        if (!Directory.Exists(sourceDir))
            return (Remap(sourceBlobPath), Remap(sourceExtractedPath));

        var destDir = _store.FileStore.GetResourceDirectory(newProjectId, newResourceId);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        return (Remap(sourceBlobPath), Remap(sourceExtractedPath));

        string? Remap(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            return Path.Combine(
                _store.FileStore.GetResourceDirectory(newProjectId, newResourceId),
                Path.GetFileName(path));
        }
    }

    private string Now() => _store.Clock.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static DateTimeOffset? TryParse(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d
            : null;
}

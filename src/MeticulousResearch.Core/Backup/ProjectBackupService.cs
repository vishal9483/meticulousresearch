using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Backup;

/// <summary>
/// The production <see cref="IProjectBackupService"/> over the <see cref="DataStore"/> (SPEC §8,
/// §9.1(9)). A backup is a zip with three parts: <c>manifest.json</c> (format + schema version and
/// project id), <c>data.json</c> (the project's rows across the six portable tables, token columns
/// verbatim), and <c>files/resources/{resourceId}/…</c> (each resource's original blob and extracted
/// text preserved in the §5 layout). Nothing from the credential vault or another project is written.
/// <para>
/// Restore validates the whole archive in memory first, then applies rows inside a single EF
/// transaction and copies files, committing only on success — so a corrupt, non-project, or
/// newer-schema archive is refused and leaves the store unchanged. Inserting rows re-fires the FTS
/// triggers owned by <c>data-store-migrations</c>, so full-text search is rebuilt from restored content.
/// </para>
/// </summary>
public sealed class ProjectBackupService : IProjectBackupService
{
    /// <summary>The current archive format version.</summary>
    public const int CurrentFormatVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string DataEntry = "data.json";
    private const string FilesPrefix = "files/resources/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly DataStore _store;

    /// <summary>Creates the backup service over the data store it snapshots/restores.</summary>
    /// <param name="store">The persistence foundation to read from and write to.</param>
    /// <exception cref="ArgumentNullException">The store is null.</exception>
    public ProjectBackupService(DataStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public void Backup(string projectId, string destinationZip)
    {
        Require(projectId, nameof(projectId));
        Require(destinationZip, nameof(destinationZip));

        var data = ReadProjectSubset(projectId)
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");

        var manifest = new ProjectBackupManifest
        {
            FormatVersion = CurrentFormatVersion,
            SchemaVersion = _store.GetSchemaVersion(),
            ProjectId = data.Project.Id,
            ProjectName = data.Project.Name,
            CreatedAt = _store.Clock.UtcNow.ToString("O"),
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationZip));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(destinationZip))
            File.Delete(destinationZip);

        using var zip = ZipFile.Open(destinationZip, ZipArchiveMode.Create);

        WriteTextEntry(zip, ManifestEntry, JsonSerializer.Serialize(manifest, JsonOptions));
        WriteTextEntry(zip, DataEntry, JsonSerializer.Serialize(data, JsonOptions));

        // Stable ordering: resources by id, blob before extracted, so identical input reproduces bytes.
        foreach (var resource in data.Resources.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            AddFileEntry(zip, resource.Id, resource.BlobPath);
            AddFileEntry(zip, resource.Id, resource.ExtractedPath);
        }
    }

    /// <inheritdoc />
    public string Restore(string sourceZip, RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Prompt)
    {
        Require(sourceZip, nameof(sourceZip));

        // Phase 1 — validate the whole archive in memory before touching the store.
        var (manifest, data, fileBytes) = ReadAndValidateArchive(sourceZip);

        var appSchema = DataStore.LatestSchemaVersion;
        if (manifest.SchemaVersion > appSchema)
            throw new IncompatibleBackupVersionException(manifest.SchemaVersion, appSchema);

        using var db = _store.CreateDbContext();

        var originalProjectId = data.Project.Id;
        var exists = db.Projects.Any(p => p.Id == originalProjectId);

        bool asCopy;
        if (exists)
        {
            asCopy = conflictPolicy switch
            {
                RestoreConflictPolicy.RestoreAsCopy => true,
                RestoreConflictPolicy.Replace => false,
                _ => throw new ProjectBackupConflictException(originalProjectId),
            };
        }
        else
        {
            asCopy = false;
        }

        // Phase 2 — apply rows in a transaction, then commit files; roll back on any failure.
        using var tx = db.Database.BeginTransaction();
        try
        {
            if (exists && !asCopy)
                DeleteExistingProject(db, originalProjectId);

            var newProjectId = asCopy ? NewId() : originalProjectId;
            ApplyRows(db, data, fileBytes, newProjectId, asCopy);

            db.SaveChanges();
            tx.Commit();
            return newProjectId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ---- Backup helpers -----------------------------------------------------------------------

    private ProjectBackupData? ReadProjectSubset(string projectId)
    {
        using var db = _store.CreateDbContext();

        var project = db.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project is null)
            return null;

        var resources = db.Resources.Where(r => r.ProjectId == projectId).ToList();
        var conversations = db.Conversations.Where(c => c.ProjectId == projectId).ToList();
        var conversationIds = conversations.Select(c => c.Id).ToList();
        var messages = db.Messages.Where(m => conversationIds.Contains(m.ConversationId)).ToList();
        var artifacts = db.Artifacts.Where(a => a.ProjectId == projectId).ToList();
        var artifactIds = artifacts.Select(a => a.Id).ToList();
        var versions = db.ArtifactVersions.Where(v => artifactIds.Contains(v.ArtifactId)).ToList();

        return new ProjectBackupData
        {
            Project = project,
            Resources = resources,
            Conversations = conversations,
            Messages = messages,
            Artifacts = artifacts,
            ArtifactVersions = versions,
        };
    }

    private static void AddFileEntry(ZipArchive zip, string resourceId, string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return;

        var entryName = $"{FilesPrefix}{resourceId}/{Path.GetFileName(sourcePath)}";
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var target = entry.Open();
        using var source = File.OpenRead(sourcePath);
        source.CopyTo(target);
    }

    private static void WriteTextEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    // ---- Restore helpers ----------------------------------------------------------------------

    private static (ProjectBackupManifest Manifest, ProjectBackupData Data, Dictionary<string, byte[]> Files)
        ReadAndValidateArchive(string sourceZip)
    {
        if (!File.Exists(sourceZip))
            throw new InvalidProjectBackupException($"Backup file '{sourceZip}' does not exist.");

        try
        {
            using var zip = ZipFile.OpenRead(sourceZip);

            var manifestEntry = zip.GetEntry(ManifestEntry)
                ?? throw new InvalidProjectBackupException("The archive is not a project backup: manifest.json is missing.");
            var dataEntry = zip.GetEntry(DataEntry)
                ?? throw new InvalidProjectBackupException("The archive is not a project backup: data.json is missing.");

            var manifest = JsonSerializer.Deserialize<ProjectBackupManifest>(ReadText(manifestEntry), JsonOptions)
                ?? throw new InvalidProjectBackupException("The backup manifest could not be read.");
            var data = JsonSerializer.Deserialize<ProjectBackupData>(ReadText(dataEntry), JsonOptions)
                ?? throw new InvalidProjectBackupException("The backup data could not be read.");

            if (string.IsNullOrWhiteSpace(manifest.ProjectId) || string.IsNullOrWhiteSpace(data.Project.Id))
                throw new InvalidProjectBackupException("The backup is missing a project id.");

            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith(FilesPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith('/'))
                    continue;
                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                files[entry.FullName] = buffer.ToArray();
            }

            return (manifest, data, files);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidProjectBackupException("The file is not a valid zip archive.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidProjectBackupException("The backup archive is corrupt.", ex);
        }
    }

    private void DeleteExistingProject(AppDbContext db, string projectId)
    {
        var conversationIds = db.Conversations.Where(c => c.ProjectId == projectId).Select(c => c.Id).ToList();
        var artifactIds = db.Artifacts.Where(a => a.ProjectId == projectId).Select(a => a.Id).ToList();

        db.Messages.RemoveRange(db.Messages.Where(m => conversationIds.Contains(m.ConversationId)));
        db.ArtifactVersions.RemoveRange(db.ArtifactVersions.Where(v => artifactIds.Contains(v.ArtifactId)));
        db.Artifacts.RemoveRange(db.Artifacts.Where(a => a.ProjectId == projectId));
        db.Conversations.RemoveRange(db.Conversations.Where(c => c.ProjectId == projectId));
        db.Resources.RemoveRange(db.Resources.Where(r => r.ProjectId == projectId));
        db.Projects.RemoveRange(db.Projects.Where(p => p.Id == projectId));
        db.SaveChanges();

        // Remove the replaced project's on-disk files so restore rewrites a clean layout.
        var dir = _store.FileStore.GetProjectDirectory(projectId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private void ApplyRows(
        AppDbContext db,
        ProjectBackupData data,
        Dictionary<string, byte[]> files,
        string newProjectId,
        bool asCopy)
    {
        var resourceMap = BuildIdMap(data.Resources.Select(r => r.Id), asCopy);
        var conversationMap = BuildIdMap(data.Conversations.Select(c => c.Id), asCopy);
        var messageMap = BuildIdMap(data.Messages.Select(m => m.Id), asCopy);
        var artifactMap = BuildIdMap(data.Artifacts.Select(a => a.Id), asCopy);
        var versionMap = BuildIdMap(data.ArtifactVersions.Select(v => v.Id), asCopy);

        var project = data.Project;
        project.Id = newProjectId;
        db.Projects.Add(project);

        foreach (var resource in data.Resources)
        {
            var originalId = resource.Id;
            resource.Id = resourceMap[originalId];
            resource.ProjectId = newProjectId;
            RestoreResourceFiles(resource, originalId, files, newProjectId);
            db.Resources.Add(resource);
        }

        foreach (var conversation in data.Conversations)
        {
            conversation.Id = conversationMap[conversation.Id];
            conversation.ProjectId = newProjectId;
            db.Conversations.Add(conversation);
        }

        foreach (var message in data.Messages)
        {
            message.Id = messageMap[message.Id];
            message.ConversationId = conversationMap[message.ConversationId];
            db.Messages.Add(message);
        }

        foreach (var artifact in data.Artifacts)
        {
            artifact.Id = artifactMap[artifact.Id];
            artifact.ProjectId = newProjectId;
            if (!string.IsNullOrEmpty(artifact.CurrentVersionId) &&
                versionMap.TryGetValue(artifact.CurrentVersionId, out var mappedCurrent))
            {
                artifact.CurrentVersionId = mappedCurrent;
            }
            db.Artifacts.Add(artifact);
        }

        foreach (var version in data.ArtifactVersions)
        {
            version.Id = versionMap[version.Id];
            version.ArtifactId = artifactMap[version.ArtifactId];
            db.ArtifactVersions.Add(version);
        }
    }

    private void RestoreResourceFiles(
        Resource resource,
        string originalResourceId,
        Dictionary<string, byte[]> files,
        string newProjectId)
    {
        var destDir = _store.FileStore.GetResourceDirectory(newProjectId, resource.Id);

        resource.BlobPath = RestoreOneFile(resource.BlobPath, originalResourceId, files, destDir);
        resource.ExtractedPath = RestoreOneFile(resource.ExtractedPath, originalResourceId, files, destDir);
    }

    private static string? RestoreOneFile(
        string? originalPath,
        string originalResourceId,
        Dictionary<string, byte[]> files,
        string destDir)
    {
        if (string.IsNullOrEmpty(originalPath))
            return originalPath;

        var fileName = Path.GetFileName(originalPath);
        var entryName = $"{FilesPrefix}{originalResourceId}/{fileName}";
        if (!files.TryGetValue(entryName, out var bytes))
            return null;

        var destPath = Path.Combine(destDir, fileName);
        File.WriteAllBytes(destPath, bytes);
        return destPath;
    }

    private static Dictionary<string, string> BuildIdMap(IEnumerable<string> ids, bool asCopy)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!map.ContainsKey(id))
                map[id] = asCopy ? NewId() : id;
        }
        return map;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must be a non-empty string.", name);
    }
}

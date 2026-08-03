using System.IO.Compression;
using System.Text;
using MeticulousResearch.Core.Backup;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Backup;

/// <summary>
/// Faithful xUnit translation of the <c>@unit @integration</c> scenarios in
/// docs/features/backup-restore/tests.md (SPEC §8, §9.1(9)). Each test seeds a real temp
/// <see cref="DataStore"/> plus on-disk resource files (the Background), backs up and/or restores
/// through <see cref="ProjectBackupService"/>, and asserts fidelity, isolation, secret-exclusion,
/// versioning, conflict handling, and transactional safety. No network is touched; time is fixed via
/// <see cref="FakeClock"/>.
/// </summary>
[Trait("Category", "integration")]
public sealed class BackupRestoreTests : IDisposable
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero));
    private readonly TempDataDirectory _sourceDir = new();
    private readonly DataStore _source;
    private readonly ProjectBackupService _sourceBackup;
    private readonly string _projectId;

    public BackupRestoreTests()
    {
        _source = new DataStore(_clock, _sourceDir.Path);
        _source.Initialize();
        _sourceBackup = new ProjectBackupService(_source);
        // Background: a project "EV Market 2026" with 3 resources, 2 conversations, 1 artifact;
        // each resource has an original blob + extracted text on disk; the artifact has an ordered
        // version history.
        _projectId = SeedBackgroundProject(_source, "EV Market 2026");
    }

    public void Dispose()
    {
        _source.ClearConnectionPool();
        _source.Dispose();
        _sourceDir.Dispose();
    }

    // ---- Backup -------------------------------------------------------------------------------

    // Scenario: Backing up a project writes a single zip
    [Fact]
    public void Backing_up_a_project_writes_a_single_zip()
    {
        var zipPath = Path.Combine(_sourceDir.Path, "ev-market.zip");

        _sourceBackup.Backup(_projectId, zipPath);

        // Then a single zip file "ev-market.zip" is written.
        Assert.True(File.Exists(zipPath));
        Assert.Equal("ev-market.zip", Path.GetFileName(zipPath));
        using var zip = ZipFile.OpenRead(zipPath);
        Assert.NotEmpty(zip.Entries);
    }

    // Scenario: The backup contains the project's DB subset
    [Fact]
    public void The_backup_contains_the_projects_DB_subset()
    {
        // A second, unrelated project whose rows must NOT appear in the backup.
        var otherId = SeedBackgroundProject(_source, "Healthcare 2026");

        var zipPath = Backup();
        var data = ReadData(zipPath);

        // Then the zip contains the project's rows across all six portable tables.
        Assert.Equal(_projectId, data.Project.Id);
        Assert.Equal(3, data.Resources.Count);
        Assert.Equal(2, data.Conversations.Count);
        Assert.NotEmpty(data.Messages);
        Assert.Single(data.Artifacts);
        Assert.Equal(3, data.ArtifactVersions.Count);

        // And it contains no rows belonging to other projects.
        Assert.All(data.Resources, r => Assert.Equal(_projectId, r.ProjectId));
        Assert.All(data.Conversations, c => Assert.Equal(_projectId, c.ProjectId));
        Assert.All(data.Artifacts, a => Assert.Equal(_projectId, a.ProjectId));
        Assert.DoesNotContain(otherId, data.Conversations.Select(c => c.ProjectId));
        Assert.DoesNotContain(otherId, data.Resources.Select(r => r.ProjectId));
    }

    // Scenario: The backup contains the project's files
    [Fact]
    public void The_backup_contains_the_projects_files()
    {
        var zipPath = Backup();
        var data = ReadData(zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        // Then the zip contains each resource's original blob and extracted text, preserving the
        // per-project resource file layout (files/resources/{resourceId}/…).
        foreach (var resource in data.Resources)
        {
            var blobEntry = zip.GetEntry($"files/resources/{resource.Id}/{Path.GetFileName(resource.BlobPath!)}");
            var extractedEntry = zip.GetEntry($"files/resources/{resource.Id}/{Path.GetFileName(resource.ExtractedPath!)}");
            Assert.NotNull(blobEntry);
            Assert.NotNull(extractedEntry);
        }
    }

    // Scenario: App-level secrets are not included in a backup
    [Fact]
    public void App_level_secrets_are_not_included_in_a_backup()
    {
        // Given an API key stored in the credential vault (and an env key) — neither is in the DB
        // subset or files, so the backup must not carry them.
        const string vaultSecret = "sk-ant-VAULT-SECRET-DEADBEEF";
        const string envSecret = "sk-ant-ENV-SECRET-CAFEBABE";
        var vault = new FakeSecureKeyStore();
        vault.Save(vaultSecret);
        var env = new FakeEnvironment();
        env.Set("ANTHROPIC_API_KEY", envSecret);

        var zipPath = Backup();

        // Then the zip contains no API key or other vault secret.
        var bytes = File.ReadAllBytes(zipPath);
        Assert.False(ContainsAscii(bytes, vaultSecret));
        Assert.False(ContainsAscii(bytes, envSecret));

        // And no Setting rows leak either (the vault-adjacent table is excluded by construction).
        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Null(zip.GetEntry("settings.json"));
    }

    // Scenario: A backup carries a schema/format version
    [Fact]
    public void A_backup_carries_a_schema_or_format_version()
    {
        var zipPath = Backup();
        var manifest = ReadManifest(zipPath);

        // Then the zip records the schema version it was produced at.
        Assert.Equal(_source.GetSchemaVersion(), manifest.SchemaVersion);
        Assert.Equal(DataStore.LatestSchemaVersion, manifest.SchemaVersion);
        Assert.True(manifest.FormatVersion > 0);
        Assert.Equal(_projectId, manifest.ProjectId);
    }

    // ---- Restore ------------------------------------------------------------------------------

    // Scenario: Restoring a backup recreates the project
    [Fact]
    public void Restoring_a_backup_recreates_the_project()
    {
        var zipPath = Backup();

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            var project = db.Projects.Single();
            // Then a project "EV Market 2026" exists with 3 resources, 2 conversations, 1 artifact.
            Assert.Equal("EV Market 2026", project.Name);
            Assert.Equal(3, db.Resources.Count(r => r.ProjectId == restoredId));
            Assert.Equal(2, db.Conversations.Count(c => c.ProjectId == restoredId));
            Assert.Equal(1, db.Artifacts.Count(a => a.ProjectId == restoredId));
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: Restore recreates resource blobs and extracted text on disk
    [Fact]
    public void Restore_recreates_resource_blobs_and_extracted_text_on_disk()
    {
        var zipPath = Backup();

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            var resources = db.Resources.Where(r => r.ProjectId == restoredId).ToList();
            Assert.Equal(3, resources.Count);
            // Then each resource's original blob and extracted text exist on disk under the layout.
            foreach (var r in resources)
            {
                Assert.True(File.Exists(r.BlobPath), $"blob missing: {r.BlobPath}");
                Assert.True(File.Exists(r.ExtractedPath), $"extracted missing: {r.ExtractedPath}");
                Assert.StartsWith(target.FileStore.GetResourceDirectory(restoredId, r.Id), r.BlobPath!);
            }
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: Restore preserves artifact version history and current version
    [Fact]
    public void Restore_preserves_artifact_version_history_and_current_version()
    {
        var zipPath = Backup();
        var originalCurrentContent = CurrentVersionContent(_source, _projectId);

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            var artifact = db.Artifacts.Single(a => a.ProjectId == restoredId);
            var versions = db.ArtifactVersions
                .Where(v => v.ArtifactId == artifact.Id)
                .OrderBy(v => v.VersionNo)
                .ToList();

            // Then the artifact has all 3 versions in order.
            Assert.Equal(3, versions.Count);
            Assert.Equal(new long[] { 1, 2, 3 }, versions.Select(v => v.VersionNo).ToArray());

            // And its current version pointer is preserved.
            Assert.NotNull(artifact.CurrentVersionId);
            var current = versions.Single(v => v.Id == artifact.CurrentVersionId);
            Assert.Equal(originalCurrentContent, current.Content);
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: Restore preserves token counts so cost recomputes
    [Fact]
    public void Restore_preserves_token_counts_so_cost_recomputes()
    {
        var prices = FixedPriceTable();
        var originalCost = new CostService(_source, prices, _clock).GetProjectCost(_projectId).Total;
        Assert.True(originalCost > 0m, "the seeded project should have a non-zero cost");

        var zipPath = Backup();

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            var restoredMessages = db.Messages
                .Where(m => db.Conversations.Where(c => c.ProjectId == restoredId).Select(c => c.Id).Contains(m.ConversationId))
                .ToList();
            var originalMessages = ReadData(zipPath).Messages;

            // Then the restored turns carry the same token counts.
            Assert.Equal(
                originalMessages.OrderBy(m => m.Content).Select(m => (m.TokensIn, m.TokensOut)).ToList(),
                restoredMessages.OrderBy(m => m.Content).Select(m => (m.TokensIn, m.TokensOut)).ToList());

            // And the consolidated cost recomputes to the same value under the same price table.
            var restoredCost = new CostService(target, prices, _clock).GetProjectCost(restoredId).Total;
            Assert.Equal(originalCost, restoredCost);
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // ---- Round-trip & integrity ---------------------------------------------------------------

    // Scenario: A backup then restore round-trips the project faithfully
    [Fact]
    public void A_backup_then_restore_round_trips_the_project_faithfully()
    {
        var zipPath = Backup();
        var original = ReadData(zipPath);

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            var project = db.Projects.Single(p => p.Id == restoredId);
            // Then the restored project matches the original in fields, resources, conversations,
            // artifacts, and versions.
            Assert.Equal(original.Project.Name, project.Name);
            Assert.Equal(original.Project.Description, project.Description);
            Assert.Equal(original.Resources.Count, db.Resources.Count(r => r.ProjectId == restoredId));
            Assert.Equal(original.Conversations.Count, db.Conversations.Count(c => c.ProjectId == restoredId));
            Assert.Equal(original.Artifacts.Count, db.Artifacts.Count(a => a.ProjectId == restoredId));

            var artifact = db.Artifacts.Single(a => a.ProjectId == restoredId);
            Assert.Equal(
                original.ArtifactVersions.OrderBy(v => v.VersionNo).Select(v => v.Content).ToList(),
                db.ArtifactVersions.Where(v => v.ArtifactId == artifact.Id).OrderBy(v => v.VersionNo).Select(v => v.Content).ToList());
            Assert.Equal(
                original.Resources.OrderBy(r => r.Title).Select(r => r.Title).ToList(),
                db.Resources.Where(r => r.ProjectId == restoredId).OrderBy(r => r.Title).Select(r => r.Title).ToList());
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: Restoring does not overwrite an unrelated existing project
    [Fact]
    public void Restoring_does_not_overwrite_an_unrelated_existing_project()
    {
        var zipPath = Backup();

        using var target = NewTargetStore(out var dir);
        try
        {
            // Given a data store containing a project "Healthcare 2026".
            var healthcareId = SeedBackgroundProject(target, "Healthcare 2026");
            var healthcareSnapshot = ProjectSnapshot(target, healthcareId);

            var backup = new ProjectBackupService(target);
            var restoredId = backup.Restore(zipPath);

            using var db = target.CreateDbContext();
            // Then "Healthcare 2026" is unchanged.
            Assert.Equal(healthcareSnapshot, ProjectSnapshot(target, healthcareId));
            // And both projects now exist.
            Assert.NotEqual(healthcareId, restoredId);
            Assert.True(db.Projects.Any(p => p.Id == healthcareId));
            Assert.True(db.Projects.Any(p => p.Id == restoredId));
            Assert.Equal(2, db.Projects.Count());
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: Restoring a backup whose id already exists is handled without clobbering
    [Fact]
    public void Restoring_a_backup_whose_id_already_exists_is_handled_without_clobbering()
    {
        var zipPath = Backup();

        // Given a data store already containing "EV Market 2026" (same id as the backup).
        // Reuse the source store itself, which already contains the project.
        var snapshotBefore = ProjectSnapshot(_source, _projectId);
        var resourceCountBefore = ResourceCount(_source, _projectId);

        // Then, with the default prompt policy, I am prompted (signalled) to choose copy or replace.
        var ex = Assert.Throws<ProjectBackupConflictException>(() => _sourceBackup.Restore(zipPath));
        Assert.Equal(_projectId, ex.ProjectId);
        Assert.Contains("copy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("replace", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And no data is silently overwritten (the existing project is untouched).
        Assert.Equal(snapshotBefore, ProjectSnapshot(_source, _projectId));
        Assert.Equal(resourceCountBefore, ResourceCount(_source, _projectId));

        // Choosing restore-as-copy makes a new project without touching the original.
        var copyId = _sourceBackup.Restore(zipPath, RestoreConflictPolicy.RestoreAsCopy);
        Assert.NotEqual(_projectId, copyId);
        Assert.Equal(snapshotBefore, ProjectSnapshot(_source, _projectId));
        Assert.Equal(3, ResourceCount(_source, copyId));
    }

    // Scenario: A corrupt or non-project zip is rejected with a clear error
    [Fact]
    public void A_corrupt_or_non_project_zip_is_rejected_with_a_clear_error()
    {
        // Given a zip that is not a valid project backup (random bytes, not even a zip).
        var bogus = Path.Combine(_sourceDir.Path, "not-a-backup.zip");
        File.WriteAllBytes(bogus, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });

        using var target = NewTargetStore(out var dir);
        try
        {
            // Given the data store contains an unrelated project to prove it's left unchanged.
            var existingId = SeedBackgroundProject(target, "Healthcare 2026");
            var snapshot = ProjectSnapshot(target, existingId);
            var backup = new ProjectBackupService(target);

            // Then restore fails with a clear, human-readable error.
            var ex = Assert.Throws<InvalidProjectBackupException>(() => backup.Restore(bogus));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));

            // A structurally-valid zip that is not a project backup is also rejected.
            var notProject = Path.Combine(_sourceDir.Path, "empty.zip");
            using (var z = ZipFile.Open(notProject, ZipArchiveMode.Create))
            {
                var e = z.CreateEntry("readme.txt");
                using var s = e.Open();
                s.Write(Encoding.UTF8.GetBytes("hello"));
            }
            Assert.Throws<InvalidProjectBackupException>(() => backup.Restore(notProject));

            // And the data store is left unchanged.
            using var db = target.CreateDbContext();
            Assert.Equal(1, db.Projects.Count());
            Assert.Equal(snapshot, ProjectSnapshot(target, existingId));
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // Scenario: A backup from a newer schema version is refused or migrated, never partially applied
    [Fact]
    public void A_backup_from_a_newer_schema_version_is_refused_or_migrated_never_partially_applied()
    {
        var zipPath = Backup();
        // Given a backup zip produced at a newer schema version than this app.
        RewriteManifestSchemaVersion(zipPath, DataStore.LatestSchemaVersion + 1);

        using var target = NewTargetStore(out var dir);
        try
        {
            var backup = new ProjectBackupService(target);

            // Then restore refuses with a clear message (migration-forward is the alternative).
            var ex = Assert.Throws<IncompatibleBackupVersionException>(() => backup.Restore(zipPath));
            Assert.Equal(DataStore.LatestSchemaVersion + 1, ex.BackupSchemaVersion);
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));

            // And the data store is left consistent (nothing was applied).
            using var db = target.CreateDbContext();
            Assert.Equal(0, db.Projects.Count());
            Assert.Equal(0, db.Resources.Count());
        }
        finally
        {
            target.ClearConnectionPool();
            dir.Dispose();
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private string Backup()
    {
        var zipPath = Path.Combine(_sourceDir.Path, $"backup-{Guid.NewGuid():N}.zip");
        _sourceBackup.Backup(_projectId, zipPath);
        return zipPath;
    }

    private DataStore NewTargetStore(out TempDataDirectory dir)
    {
        dir = new TempDataDirectory();
        var store = new DataStore(_clock, dir.Path);
        store.Initialize();
        return store;
    }

    private static DictionaryCostPriceSource FixedPriceTable()
    {
        var prices = new DictionaryCostPriceSource();
        prices.SetRates("claude-opus-5", new CostRates(5m, 25m, 0.5m, 6.25m));
        prices.SetRates("claude-sonnet-5", new CostRates(3m, 15m, 0.3m, 3.75m));
        return prices;
    }

    private ProjectBackupData ReadData(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("data.json")!;
        using var stream = entry.Open();
        return System.Text.Json.JsonSerializer.Deserialize<ProjectBackupData>(stream)!;
    }

    private ProjectBackupManifest ReadManifest(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("manifest.json")!;
        using var stream = entry.Open();
        return System.Text.Json.JsonSerializer.Deserialize<ProjectBackupManifest>(stream)!;
    }

    private static void RewriteManifestSchemaVersion(string zipPath, int newSchemaVersion)
    {
        ProjectBackupManifest manifest;
        using (var zip = ZipFile.OpenRead(zipPath))
        using (var stream = zip.GetEntry("manifest.json")!.Open())
        {
            manifest = System.Text.Json.JsonSerializer.Deserialize<ProjectBackupManifest>(stream)!;
        }
        manifest.SchemaVersion = newSchemaVersion;

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        archive.GetEntry("manifest.json")!.Delete();
        var entry = archive.CreateEntry("manifest.json");
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(manifest));
        s.Write(bytes, 0, bytes.Length);
    }

    private static bool ContainsAscii(byte[] haystack, string needle)
    {
        var pattern = Encoding.ASCII.GetBytes(needle);
        for (var i = 0; i + pattern.Length <= haystack.Length; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (haystack[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private static string CurrentVersionContent(DataStore store, string projectId)
    {
        using var db = store.CreateDbContext();
        var artifact = db.Artifacts.Single(a => a.ProjectId == projectId);
        return db.ArtifactVersions.Single(v => v.Id == artifact.CurrentVersionId).Content;
    }

    private static int ResourceCount(DataStore store, string projectId)
    {
        using var db = store.CreateDbContext();
        return db.Resources.Count(r => r.ProjectId == projectId);
    }

    private static string ProjectSnapshot(DataStore store, string projectId)
    {
        using var db = store.CreateDbContext();
        var p = db.Projects.Single(x => x.Id == projectId);
        var resources = string.Join("|", db.Resources.Where(r => r.ProjectId == projectId).ToList()
            .OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => r.Id + ":" + r.Title));
        var conversations = string.Join("|", db.Conversations.Where(c => c.ProjectId == projectId).ToList()
            .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id + ":" + c.Title));
        return $"{p.Id}|{p.Name}|{p.Description}|{resources}|{conversations}";
    }

    /// <summary>
    /// Seeds a project matching the Background: 3 resources (each with an on-disk blob + extracted
    /// text), 2 conversations (each with a token-bearing assistant turn), and 1 artifact with an
    /// ordered 3-version history and a current-version pointer.
    /// </summary>
    private string SeedBackgroundProject(DataStore store, string name)
    {
        var now = _clock.UtcNow.ToString("O");
        var projectId = Guid.NewGuid().ToString("N");

        using (var db = store.CreateDbContext())
        {
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = name,
                Description = name + " description",
                CreatedAt = now,
                UpdatedAt = now,
            });

            for (var i = 0; i < 3; i++)
            {
                var resourceId = Guid.NewGuid().ToString("N");
                var dir = store.FileStore.GetResourceDirectory(projectId, resourceId);
                var blobPath = Path.Combine(dir, "original.txt");
                var extractedPath = Path.Combine(dir, "extracted.txt");
                var body = $"{name} resource {i} body text";
                File.WriteAllText(blobPath, $"BLOB[{name}:{i}]");
                File.WriteAllText(extractedPath, body);

                db.Resources.Add(new Resource
                {
                    Id = resourceId,
                    ProjectId = projectId,
                    Title = $"Resource {i}",
                    Type = "file",
                    BlobPath = blobPath,
                    ExtractedPath = extractedPath,
                    ExtractedText = body,
                    Enabled = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            for (var c = 0; c < 2; c++)
            {
                var conversationId = Guid.NewGuid().ToString("N");
                db.Conversations.Add(new Conversation
                {
                    Id = conversationId,
                    ProjectId = projectId,
                    Title = $"Conversation {c}",
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                db.Messages.Add(new Message
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ConversationId = conversationId,
                    Role = "assistant",
                    Content = $"{name} answer {c}",
                    Model = "claude-opus-5",
                    TokensIn = 100_000 + c,
                    TokensOut = 20_000 + c,
                    CreatedAt = now,
                });
            }

            var artifactId = Guid.NewGuid().ToString("N");
            string? currentVersionId = null;
            var artifact = new Artifact
            {
                Id = artifactId,
                ProjectId = projectId,
                Title = "Report",
                Type = "doc",
                CreatedAt = now,
                UpdatedAt = now,
            };
            for (var v = 1; v <= 3; v++)
            {
                var versionId = Guid.NewGuid().ToString("N");
                currentVersionId = versionId;
                db.ArtifactVersions.Add(new ArtifactVersion
                {
                    Id = versionId,
                    ArtifactId = artifactId,
                    VersionNo = v,
                    Content = $"{name} artifact version {v}",
                    ContentFormat = "markdown",
                    CreatedBy = "claude",
                    CreatedAt = now,
                });
            }
            artifact.CurrentVersionId = currentVersionId;
            db.Artifacts.Add(artifact);

            db.SaveChanges();
        }

        return projectId;
    }
}

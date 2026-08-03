using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Projects;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/projects-crud/tests.md.
/// These are @unit and run in the headless gate; they touch a temp SQLite database so they carry
/// no excluded Category trait (TESTING-STRATEGY §4 — @unit may touch a temp store).
/// </summary>
public sealed class ProjectServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-projects-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _service = new ProjectService(_store, settings);
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // ---------------------------------------------------------------- Create

    // Scenario: Creating a blank project with a name
    [Fact]
    public void Creating_a_blank_project_with_a_name()
    {
        // When I create a project named "Automotive EV 2026"
        var project = _service.Create("Automotive EV 2026");

        // Then a project "Automotive EV 2026" exists
        Assert.NotNull(_service.Get(project.Id));
        Assert.Equal("Automotive EV 2026", _service.Get(project.Id)!.Name);

        // And its created_at and updated_at are set
        Assert.False(string.IsNullOrWhiteSpace(project.CreatedAt));
        Assert.False(string.IsNullOrWhiteSpace(project.UpdatedAt));

        // And its default model is the app default "claude-opus-5"
        Assert.Equal("claude-opus-5", project.DefaultModel);
        Assert.Equal(SettingsService.DefaultModelValue, project.DefaultModel);

        // And it is not archived
        Assert.False(project.Archived);
    }

    // Scenario: Project name is required
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Project_name_is_required(string emptyName)
    {
        // When I try to create a project with an empty name → validation error (exception)
        Assert.Throws<ArgumentException>(() => _service.Create(emptyName));

        // And no project is created
        Assert.Empty(_service.List(includeArchived: true));
    }

    // ---------------------------------------------------------------- Fields & custom instructions

    // Scenario: A project stores all specified fields
    [Fact]
    public void A_project_stores_all_specified_fields()
    {
        // Given a new project — when I set description, custom instructions, default model, color
        var project = _service.Create(
            name: "EV Forecast",
            description: "10-year EV forecast",
            customInstructions: "Use house style, formal tone",
            defaultModel: "claude-sonnet-5",
            color: "navy");

        // Then those values persist and are re-read after reopening the project
        var reread = _service.Get(project.Id)!;
        Assert.Equal("10-year EV forecast", reread.Description);
        Assert.Equal("Use house style, formal tone", reread.CustomInstructions);
        Assert.Equal("claude-sonnet-5", reread.DefaultModel);
        Assert.Equal("navy", reread.Color);
        Assert.Equal("EV Forecast", reread.Name);

        // A fresh service instance over the same store re-reads the same values (survives reopen).
        var fresh = new ProjectService(_store, new SettingsService(_store));
        var afterReopen = fresh.Get(project.Id)!;
        Assert.Equal("navy", afterReopen.Color);
        Assert.Equal("Use house style, formal tone", afterReopen.CustomInstructions);
    }

    // Scenario: Custom instructions are available for grounding
    [Fact]
    public void Custom_instructions_are_available_for_grounding()
    {
        // Given a project with custom instructions "Always cite sources"
        var project = _service.Create("Grounded", customInstructions: "Always cite sources");

        // When the project's system-prompt context is assembled
        var context = _service.BuildSystemPromptContext(project.Id);

        // Then it includes "Always cite sources"
        Assert.Contains("Always cite sources", context);
    }

    // ---------------------------------------------------------------- Rename / duplicate / archive / delete

    // Scenario: Renaming a project updates its name and timestamp
    [Fact]
    public void Renaming_a_project_updates_its_name_and_timestamp()
    {
        // Given a project named "Semiconductors 2026"
        var project = _service.Create("Semiconductors 2026");
        var before = project.UpdatedAt;

        // (advance the clock so the new updated_at is strictly newer)
        _clock.Advance(TimeSpan.FromMinutes(5));

        // When I rename it to "Semiconductors 2027"
        var renamed = _service.Rename(project.Id, "Semiconductors 2027");

        // Then the project is named "Semiconductors 2027"
        Assert.Equal("Semiconductors 2027", _service.Get(project.Id)!.Name);

        // And its updated_at is newer than before
        Assert.True(
            DateTimeOffset.Parse(renamed.UpdatedAt) > DateTimeOffset.Parse(before),
            "updated_at should be strictly newer after rename.");
    }

    // Scenario: Duplicating a project copies its configuration and resources
    [Fact]
    public void Duplicating_a_project_copies_configuration_and_resources()
    {
        // Given a project "Base Study" with 2 resources and custom instructions
        var project = _service.Create(
            "Base Study",
            customInstructions: "Follow the base playbook",
            defaultModel: "claude-sonnet-5");
        SeedResource(project.Id, "Res A", body: "alpha content");
        SeedResource(project.Id, "Res B", body: "beta content");

        // seed a conversation and an artifact that must NOT be copied
        SeedConversation(project.Id, "Chat 1");
        SeedArtifact(project.Id, "Doc 1");

        // When I duplicate it as "Base Study (copy)"
        var copy = _service.Duplicate(project.Id, "Base Study (copy)");

        // Then a new project "Base Study (copy)" exists
        Assert.NotEqual(project.Id, copy.Id);
        Assert.Equal("Base Study (copy)", _service.Get(copy.Id)!.Name);

        // And it has the same custom instructions and default model
        var rereadCopy = _service.Get(copy.Id)!;
        Assert.Equal("Follow the base playbook", rereadCopy.CustomInstructions);
        Assert.Equal("claude-sonnet-5", rereadCopy.DefaultModel);

        // And it has copies of the 2 resources
        var copiedResources = ResourcesOf(copy.Id);
        Assert.Equal(2, copiedResources.Count);
        Assert.Equal(new[] { "Res A", "Res B" }, copiedResources.Select(r => r.Title).OrderBy(x => x).ToArray());
        // the resources are genuinely new rows (distinct ids) whose files were copied to disk
        Assert.All(copiedResources, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.BlobPath));
            Assert.True(File.Exists(r.BlobPath!), $"Expected copied blob for '{r.Title}' at {r.BlobPath}.");
        });

        // And conversations and artifacts are NOT copied
        Assert.Empty(ConversationsOf(copy.Id));
        Assert.Empty(ArtifactsOf(copy.Id));
    }

    // Scenario: Archiving hides a project from the default list
    [Fact]
    public void Archiving_hides_a_project_from_the_default_list()
    {
        // Given an active project "Old Study"
        var project = _service.Create("Old Study");

        // When I archive it
        _service.Archive(project.Id);

        // Then it does not appear in the default Projects home list
        Assert.DoesNotContain(_service.List(), p => p.Id == project.Id);

        // And it appears when the "Show archived" toggle is on
        Assert.Contains(_service.List(includeArchived: true), p => p.Id == project.Id);
    }

    // Scenario: Unarchiving restores a project to the default list
    [Fact]
    public void Unarchiving_restores_a_project_to_the_default_list()
    {
        // Given an archived project "Old Study"
        var project = _service.Create("Old Study");
        _service.Archive(project.Id);
        Assert.DoesNotContain(_service.List(), p => p.Id == project.Id);

        // When I unarchive it
        _service.Unarchive(project.Id);

        // Then it appears in the default Projects home list
        Assert.Contains(_service.List(), p => p.Id == project.Id);
    }

    // Scenario: Deleting a project removes it and its files
    [Fact]
    public void Deleting_a_project_removes_it_and_its_files()
    {
        // Given a project "Scratch" with resources on disk
        var project = _service.Create("Scratch");
        SeedResource(project.Id, "Doc", body: "scratch body");
        var projectDir = Path.Combine(_store.FileStore.DataDirectory, "projects", project.Id);
        Assert.True(Directory.Exists(projectDir));

        // When I delete it and confirm
        _service.Delete(project.Id);

        // Then the project no longer exists
        Assert.Null(_service.Get(project.Id));

        // And its "projects/{id}" directory is removed
        Assert.False(Directory.Exists(projectDir));
    }

    // ---------------------------------------------------------------- Dashboard & search

    // Scenario: Project dashboard reports counts and last activity
    [Fact]
    public void Project_dashboard_reports_counts_and_last_activity()
    {
        // Given a project with 3 resources, 2 conversations, and 1 artifact
        var project = _service.Create("Dashboard Study");
        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            SeedResource(project.Id, $"Res {i}", body: $"body {i}");
        }
        for (var i = 0; i < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            SeedConversation(project.Id, $"Chat {i}");
        }
        _clock.Advance(TimeSpan.FromMinutes(1));
        var lastStamp = _clock.UtcNow;
        SeedArtifact(project.Id, "Doc");

        // When I view the project dashboard
        var dashboard = _service.GetDashboard(project.Id);

        // Then it shows resource count 3, conversation count 2, artifact count 1
        Assert.Equal(3, dashboard.ResourceCount);
        Assert.Equal(2, dashboard.ConversationCount);
        Assert.Equal(1, dashboard.ArtifactCount);

        // And it shows the most recent activity timestamp (the last-seeded child's timestamp)
        Assert.NotNull(dashboard.LastActivity);
        Assert.Equal(lastStamp, dashboard.LastActivity!.Value);
    }

    // Scenario Outline: Searching projects filters by name or description
    [Theory]
    [InlineData("2026", "Healthcare 2026, Energy 2026")]
    [InlineData("Automotive", "Automotive 2025")]
    [InlineData("zzz", "")]
    public void Searching_projects_filters_by_name_or_description(string query, string expected)
    {
        // Given projects "Healthcare 2026", "Energy 2026", and "Automotive 2025"
        _service.Create("Healthcare 2026");
        _service.Create("Energy 2026");
        _service.Create("Automotive 2025");

        // When I search projects for "<query>"
        var results = _service.Search(query);

        // Then the results are "<results>"
        var expectedNames = expected.Length == 0
            ? Array.Empty<string>()
            : expected.Split(", ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            expectedNames.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            results.Select(r => r.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    // ---------------------------------------------------------------- Seeding helpers

    private void SeedResource(string projectId, string title, string body)
    {
        var resourceId = Guid.NewGuid().ToString("N");
        var dir = _store.FileStore.GetResourceDirectory(projectId, resourceId);
        var blobPath = Path.Combine(dir, "original.txt");
        File.WriteAllText(blobPath, body);

        using var db = _store.CreateDbContext();
        db.Resources.Add(new Resource
        {
            Id = resourceId,
            ProjectId = projectId,
            Title = title,
            Type = "text",
            BlobPath = blobPath,
            Enabled = true,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private void SeedConversation(string projectId, string title)
    {
        using var db = _store.CreateDbContext();
        db.Conversations.Add(new Conversation
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            Title = title,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private void SeedArtifact(string projectId, string title)
    {
        using var db = _store.CreateDbContext();
        db.Artifacts.Add(new Artifact
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            Title = title,
            Type = "doc",
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private List<Resource> ResourcesOf(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Resources.Where(r => r.ProjectId == projectId).ToList();
    }

    private List<Conversation> ConversationsOf(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Conversations.Where(c => c.ProjectId == projectId).ToList();
    }

    private List<Artifact> ArtifactsOf(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Artifacts.Where(a => a.ProjectId == projectId).ToList();
    }
}

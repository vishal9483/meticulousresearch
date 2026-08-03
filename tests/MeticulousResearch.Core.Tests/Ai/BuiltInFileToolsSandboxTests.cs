using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Ai.Tools;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Vision;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Resources;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Faithful xUnit translation of every scenario in docs/features/builtin-file-tools-sandbox/tests.md
/// (SPEC §7.4 curated tool set + sandbox, §3.4 writes land as artifact versions, §3.2.1 images as
/// vision blocks). @unit / @integration scenarios run in the headless gate over a temp SQLite store +
/// file layout with no network. Writes route through a <see cref="FakeArtifactService"/> standing in
/// for the M3 versioning implementation the artifact service contract (owned by ai-gateway) will
/// realize.
/// </summary>
public sealed class BuiltInFileToolsSandboxTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly VisionContentAssembler _vision = new();
    private readonly FakeArtifactService _artifacts = new();
    private readonly string _projectId;

    public BuiltInFileToolsSandboxTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-tool-sandbox-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        _projects = new ProjectService(_store, new SettingsService(_store));
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _projectId = _projects.Create("P").Id;
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

    private ToolCallLog _log = new();

    private ProjectToolInvoker NewInvoker(string? projectId = null)
    {
        var id = projectId ?? _projectId;
        var sandbox = new ProjectSandbox(_store.FileStore.GetProjectDirectory(id));
        return new ProjectToolInvoker(id, sandbox, _store, _resources, _vision, _artifacts, _log);
    }

    // ---- The fixed curated tool set (§7.4) ----

    // Scenario: Exactly the curated tools are exposed to the model loop
    [Fact]
    public void Exactly_the_curated_tools_are_exposed_to_the_model_loop()
    {
        // Then it contains exactly: Glob, Grep, Read, Edit, Write, emit_artifact, update_artifact
        Assert.Equal(
            new[] { "Glob", "Grep", "Read", "Edit", "Write", "emit_artifact", "update_artifact" },
            BuiltInToolSet.ToolNames);

        // And no other tools are available
        Assert.Equal(7, BuiltInToolSet.ToolNames.Count);
        Assert.False(BuiltInToolSet.Contains("Bash"));
        Assert.False(BuiltInToolSet.Contains("WebFetch"));
        Assert.False(BuiltInToolSet.Contains("MCP"));
    }

    // Scenario: Glob finds files by pattern within the project sandbox
    [Fact]
    public void Glob_finds_files_by_pattern_within_the_project_sandbox()
    {
        // Given project "P" with resource files "filing.pdf" and "notes.txt"
        var root = _store.FileStore.GetProjectDirectory(_projectId);
        var resourcesDir = _store.FileStore.GetProjectResourcesDirectory(_projectId);
        File.WriteAllText(Path.Combine(resourcesDir, "filing.pdf"), "pdf bytes");
        File.WriteAllText(Path.Combine(resourcesDir, "notes.txt"), "some notes");

        // A file outside the project sandbox that must never be listed.
        var outside = Path.Combine(_dataDir, "outside.txt");
        File.WriteAllText(outside, "secret");

        // When the model calls Glob with pattern "*.txt"
        var results = NewInvoker().Glob("*.txt");

        // Then the result lists "notes.txt"
        Assert.Contains(results, r => r.EndsWith("notes.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(results, r => r.EndsWith("filing.pdf", StringComparison.Ordinal));

        // And does not list files outside the project sandbox
        Assert.DoesNotContain(results, r => r.Contains("outside", StringComparison.Ordinal));
    }

    // Scenario: Grep searches content across resource text and artifact versions
    [Fact]
    public void Grep_searches_content_across_resource_text_and_artifact_versions()
    {
        // Given project "P" containing the phrase "market share" in a resource
        var resource = _resources.AddText(_projectId, "Analysis", "The market share grew to 34% in 2025.");

        // When the model calls Grep for "market share"
        var matches = NewInvoker().Grep("market share");

        // Then the match from that resource is returned
        Assert.Contains(matches, m => m.Source == "resource" && m.Id == resource.Id);
        Assert.Contains(matches, m => m.Snippet.Contains("market share", StringComparison.OrdinalIgnoreCase));
    }

    // Scenario: Read returns a resource's extracted text
    [Fact]
    public void Read_returns_a_resources_extracted_text()
    {
        // Given project "P" with a resource whose extracted text is "TAM is $12B"
        var resource = _resources.AddText(_projectId, "TAM", "TAM is $12B");

        // When the model calls Read on that resource
        var result = NewInvoker().Read(resource.Id);

        // Then it returns "TAM is $12B"
        Assert.False(result.IsImage);
        Assert.Equal("TAM is $12B", result.Text);
    }

    // Scenario: Read returns an image resource as a vision content block
    [Fact]
    public void Read_returns_an_image_resource_as_a_vision_content_block()
    {
        // Given project "P" with an image resource
        var sourceDir = Path.Combine(_dataDir, "src");
        Directory.CreateDirectory(sourceDir);
        var imagePath = ImageFixtures.Write(sourceDir, "chart", "png", width: 6, height: 4);
        var image = _resources.AddImage(_projectId, imagePath);

        // When the model calls Read on the image
        var result = NewInvoker().Read(image.Id);

        // Then the result is an image content block (not raw bytes as text)
        Assert.True(result.IsImage);
        Assert.Null(result.Text);
        Assert.NotNull(result.Image);
        Assert.Equal("image/png", result.Image!.MediaType);
        Assert.False(string.IsNullOrEmpty(result.Image.Base64Data));
    }

    // ---- Writes land as artifact versions (§7.4 / §3.4) ----

    // Scenario: Write creates a new artifact rather than overwriting a file
    [Fact]
    public void Write_creates_a_new_artifact_rather_than_overwriting_a_file()
    {
        // A pre-existing file in the sandbox that must not be silently overwritten.
        var resourcesDir = _store.FileStore.GetProjectResourcesDirectory(_projectId);
        var existing = Path.Combine(resourcesDir, "existing.txt");
        File.WriteAllText(existing, "ORIGINAL");

        // When the model calls Write to author a document
        var result = NewInvoker().Write("Q3 Report", "# Q3 Report\nBody.");

        // Then a new artifact version is created via the artifact service
        Assert.Single(_artifacts.EmitCommands);
        Assert.Equal(1, result.Version);
        Assert.Equal("# Q3 Report\nBody.", _artifacts.EmitCommands[0].Content);
        Assert.Equal(_projectId, _artifacts.EmitCommands[0].ProjectId);

        // And no existing file is silently overwritten
        Assert.Equal("ORIGINAL", File.ReadAllText(existing));
    }

    // Scenario: Edit on an existing artifact creates a new version, preserving the prior one
    [Fact]
    public void Edit_on_an_existing_artifact_creates_a_new_version_preserving_the_prior_one()
    {
        // Given project "P" with an artifact at version 1
        var artifactId = _artifacts.SeedArtifact("Brief", "v1 content");

        // When the model calls Edit on that artifact
        var result = NewInvoker().Edit(artifactId, "v2 content");

        // Then a new version 2 is created
        Assert.Equal(2, result.Version);

        // And version 1 still exists unchanged
        var versions = _artifacts.VersionsOf(artifactId);
        Assert.Contains(versions, v => v.Version == 1 && v.Content == "v1 content");
        Assert.Contains(versions, v => v.Version == 2 && v.Content == "v2 content");
    }

    // Scenario: emit_artifact / update_artifact go through the artifact service contract
    [Fact]
    public void Emit_and_update_artifact_go_through_the_artifact_service_contract()
    {
        var invoker = NewInvoker();

        // Given the model calls emit_artifact with a title and content
        var emitted = invoker.EmitArtifact("Findings", "Initial findings.");

        // Then the artifact service receives a structured create request
        Assert.Single(_artifacts.EmitCommands);
        Assert.Equal("Findings", _artifacts.EmitCommands[0].Title);
        Assert.Equal("Initial findings.", _artifacts.EmitCommands[0].Content);

        // And a subsequent update_artifact is received as a structured update
        invoker.UpdateArtifact(emitted.ArtifactId, "Revised findings.");
        Assert.Single(_artifacts.UpdateCommands);
        Assert.Equal(emitted.ArtifactId, _artifacts.UpdateCommands[0].ArtifactId);
        Assert.Equal("Revised findings.", _artifacts.UpdateCommands[0].Content);
    }

    // ---- Sandboxing (§7.4) ----

    // Scenario Outline: Path traversal outside the project sandbox is rejected
    [Theory]
    [InlineData("../otherproject/secret.txt")]
    [InlineData("../../Windows/System32/x.dll")]
    [InlineData("/db.sqlite")]
    [InlineData(@"C:\Users\me\Documents\a.docx")]
    public void Path_traversal_outside_the_project_sandbox_is_rejected(string path)
    {
        // A sentinel file outside the sandbox that must remain untouched.
        var outsideDir = Path.Combine(_dataDir, "external");
        Directory.CreateDirectory(outsideDir);
        var sentinel = Path.Combine(outsideDir, "sentinel.txt");
        File.WriteAllText(sentinel, "UNTOUCHED");

        var invoker = NewInvoker();

        // When the model targets the path "<path>"
        // Then the call is rejected with a sandbox-violation error
        Assert.Throws<SandboxViolationException>(() => invoker.Read(path));

        // And nothing outside "projects/P" is read or written
        Assert.Equal("UNTOUCHED", File.ReadAllText(sentinel));
        Assert.Contains(_log.Calls, c => c.Tool == "Read" && !c.Success);
    }

    // Scenario: Tools cannot reach another project's directory
    [Fact]
    public void Tools_cannot_reach_another_projects_directory()
    {
        // Given projects "P" and "Q"
        var qId = _projects.Create("Q").Id;
        var qDir = _store.FileStore.GetProjectDirectory(qId);
        var qFile = Path.Combine(qDir, "q-secret.txt");
        File.WriteAllText(qFile, "Q-SECRET");

        var invoker = NewInvoker();

        // When a tool call in project "P" targets a file in "projects/Q"
        var relativeToQ = Path.Combine("..", qId, "q-secret.txt");

        // Then the call is rejected
        Assert.Throws<SandboxViolationException>(() => invoker.Read(relativeToQ));

        // And "Q" is untouched
        Assert.Equal("Q-SECRET", File.ReadAllText(qFile));
    }

    // Scenario: Tools cannot touch the SQLite database or app files
    [Fact]
    public void Tools_cannot_touch_the_sqlite_database_or_app_files()
    {
        var invoker = NewInvoker();

        // When it targets "db.sqlite" ...
        Assert.Throws<SandboxViolationException>(() => invoker.Read(_store.DatabasePath));

        // ... or a path outside any project
        var outsideAnyProject = Path.Combine("..", "..", "logs", "app.log");
        Assert.Throws<SandboxViolationException>(() => invoker.Read(outsideAnyProject));
    }

    // ---- Transparency (§7.4) ----

    // Scenario: Every tool call is logged and made visible in the conversation
    [Fact]
    public void Every_tool_call_is_logged_and_made_visible_in_the_conversation()
    {
        _resources.AddText(_projectId, "Doc", "quarterly market share figures");
        var invoker = NewInvoker();

        // Given a generation that calls Grep then Write
        invoker.Grep("market share");
        invoker.Write("Summary", "The summary.");

        // Then the conversation records the Grep call and the Write call
        Assert.Equal(2, _log.Calls.Count);
        var grep = _log.Calls[0];
        var write = _log.Calls[1];
        Assert.Equal("Grep", grep.Tool);
        Assert.Equal("Write", write.Tool);

        // And each is visible to the user with its inputs and outcome
        Assert.Contains(grep.Inputs, kv => kv.Key == "query" && kv.Value == "market share");
        Assert.False(string.IsNullOrWhiteSpace(grep.Outcome));
        Assert.Contains(write.Inputs, kv => kv.Key == "title" && kv.Value == "Summary");
        Assert.False(string.IsNullOrWhiteSpace(write.Outcome));
    }
}

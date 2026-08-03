using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Search;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Artifacts;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> / <c>@unit @integration</c> scenario in
/// docs/features/artifact-creation/tests.md (SPEC §3.4, §5, §7.4). None carry an excluded
/// <c>Category</c> trait, so they run in the headless gate over a real <see cref="ArtifactService"/>
/// and temp SQLite store; AI generation is served by the deterministic <see cref="FakeChatService"/>
/// and timestamps come from an injected clock (TESTING-STRATEGY §4).
///
/// Background: a project "EV Batteries 2026" with 2 enabled resources; AI generation served by a
/// deterministic FakeChatService.
/// </summary>
public sealed class ArtifactServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _conversations;
    private readonly ArtifactService _artifacts;
    private readonly SearchService _search;
    private readonly string _projectId;
    private readonly Resource _resourceA;
    private readonly Resource _resourceB;

    public ArtifactServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-artifact-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _conversations = new ConversationService(_store, _chat, _projects, _resources, _clock);
        _artifacts = new ArtifactService(_store, _chat, _clock);
        _search = new SearchService(_store);

        _projectId = _projects.Create("EV Batteries 2026").Id;
        _resourceA = _resources.AddText(_projectId, "Cell chemistry", "NMC vs LFP tradeoffs.");
        _resourceB = _resources.AddText(_projectId, "Demand model", "EV demand grows through 2026.");
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

    private ArtifactVersion CurrentVersion(Artifact artifact)
    {
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking()
            .Single(v => v.Id == artifact.CurrentVersionId);
    }

    private IReadOnlyList<ChatResource> BothResources() => new[]
    {
        new ChatResource(_resourceA.Id, _resourceA.Title, "NMC vs LFP tradeoffs."),
        new ChatResource(_resourceB.Id, _resourceB.Title, "EV demand grows through 2026."),
    };

    // ----- Types -----

    // Scenario Outline: An artifact can be created for each supported type
    [Theory]
    [InlineData("doc", "Exec Summary", "markdown")]
    [InlineData("text", "Raw Notes", "text")]
    [InlineData("code", "Sizing Script", "code")]
    [InlineData("table", "Forecast Table", "csv")]
    [InlineData("diagram", "Value Chain", "mermaid")]
    public void An_artifact_can_be_created_for_each_supported_type(string type, string title, string format)
    {
        // When I create a "<type>" artifact titled "<title>"
        var artifact = _artifacts.Create(_projectId, type, title);

        // Then an artifact "<title>" of type "<type>" exists
        var loaded = _artifacts.Get(artifact.Id);
        Assert.NotNull(loaded);
        Assert.Equal(title, loaded!.Title);
        Assert.Equal(type, loaded.Type);

        // And its content_format is "<format>"
        Assert.Equal(format, CurrentVersion(artifact).ContentFormat);
    }

    // Scenario: A diagram artifact stores Mermaid source
    [Fact]
    public void A_diagram_artifact_stores_Mermaid_source()
    {
        // Given a "diagram" artifact
        var artifact = _artifacts.Create(_projectId, "diagram", "Value Chain");

        // When its content is set to a Mermaid flowchart
        const string mermaid = "flowchart TD\n  A[Mine] --> B[Refine] --> C[Cell]";
        _artifacts.SetContent(artifact.Id, mermaid);

        var current = CurrentVersion(_artifacts.Get(artifact.Id)!);

        // Then the content_format is "mermaid"
        Assert.Equal("mermaid", current.ContentFormat);

        // And the stored content is the raw Mermaid source (not a rendered image)
        Assert.Equal(mermaid, current.Content);
        Assert.StartsWith("flowchart", current.Content);
    }

    // Scenario: An unknown artifact type is rejected
    [Fact]
    public void An_unknown_artifact_type_is_rejected()
    {
        // When I try to create an artifact of type "slide-deck"
        Assert.Throws<ArtifactValidationException>(() => _artifacts.Create(_projectId, "slide-deck", "Deck"));

        // And no artifact is created
        Assert.Empty(_artifacts.List(_projectId));
    }

    // ----- Creation path 1 — promote an assistant turn -----

    // Scenario: Promoting an assistant turn creates a doc artifact from its content
    [Fact]
    public async Task Promoting_an_assistant_turn_creates_a_doc_artifact_from_its_content()
    {
        // Given a conversation with an assistant turn containing a market-sizing summary
        const string summary = "The addressable EV battery market is ~1.2 TWh in 2026.";
        _chat.WithCompletionText(summary).WithUsage(10, 20);
        var conversation = _conversations.Create(_projectId);
        var turn = await _conversations.Ask(
            conversation.Id, "Size the market", "claude-opus-5", BothResources());

        // When I promote that turn to an artifact titled "Market Sizing"
        var artifact = _artifacts.PromoteTurn(turn.Id, "Market Sizing");

        // Then an artifact "Market Sizing" of type "doc" exists
        var loaded = _artifacts.Get(artifact.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Market Sizing", loaded!.Title);
        Assert.Equal("doc", loaded.Type);

        var version = CurrentVersion(loaded);

        // And its first version content equals the turn's content
        Assert.Equal(1, version.VersionNo);
        Assert.Equal(summary, version.Content);

        // And the version's created_by is "claude"
        Assert.Equal("claude", version.CreatedBy);

        // And the version records the turn's model and in-scope resources
        Assert.Equal("claude-opus-5", version.Model);
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Contains(_resourceB.Id, version.ResourceScopeJson!);
    }

    // Scenario: Promoting a turn copies its usage onto the version
    [Fact]
    public async Task Promoting_a_turn_copies_its_usage_onto_the_version()
    {
        // Given an assistant turn with tokens_in 1200 and tokens_out 800
        _chat.WithCompletionText("summary").WithUsage(1200, 800);
        var conversation = _conversations.Create(_projectId);
        var turn = await _conversations.Ask(conversation.Id, "Summarize", "claude-opus-5");

        // When I promote that turn to an artifact
        var artifact = _artifacts.PromoteTurn(turn.Id, "Summary");

        // Then the created version records tokens_in 1200 and tokens_out 800
        var version = CurrentVersion(artifact);
        Assert.Equal(1200, version.TokensIn);
        Assert.Equal(800, version.TokensOut);
    }

    // ----- Creation path 2 — generate directly -----

    // Scenario: Generating an artifact directly from a prompt and model
    [Fact]
    public async Task Generating_an_artifact_directly_from_a_prompt_and_model()
    {
        // Given the "New artifact" flow is open (deterministic FakeChatService emits the content)
        _chat.WithCompletionText("A competitive landscape overview.").WithUsage(10, 20);

        // When I enter the prompt, select model "claude-opus-5" and include both resources, and generate
        var request = new GenerateArtifactRequest
        {
            Type = "doc",
            Title = "Competitive Landscape",
            Prompt = "Draft a competitive landscape overview",
            Model = "claude-opus-5",
            Resources = BothResources(),
        };
        var artifact = await _artifacts.Generate(_projectId, request);

        // Then an artifact is created from the FakeChatService's emitted content
        var version = CurrentVersion(artifact);
        Assert.Equal("A competitive landscape overview.", version.Content);

        // And its first version records the prompt, model "claude-opus-5", and the 2 in-scope resource ids
        Assert.Equal(1, version.VersionNo);
        Assert.Equal("Draft a competitive landscape overview", version.Prompt);
        Assert.Equal("claude-opus-5", version.Model);
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Contains(_resourceB.Id, version.ResourceScopeJson!);

        // And created_by is "claude"
        Assert.Equal("claude", version.CreatedBy);
    }

    // Scenario: Direct generation records usage and cost tokens on the version
    [Fact]
    public async Task Direct_generation_records_usage_and_cost_tokens_on_the_version()
    {
        // Given a FakeChatService scripted to return tokens_in 2000 and tokens_out 1500
        _chat.WithCompletionText("draft").WithUsage(2000, 1500);

        // When I generate an artifact directly
        var request = new GenerateArtifactRequest
        {
            Title = "Draft",
            Prompt = "Draft something",
            Model = "claude-opus-5",
        };
        var artifact = await _artifacts.Generate(_projectId, request);

        // Then the version records tokens_in 2000 and tokens_out 1500
        var version = CurrentVersion(artifact);
        Assert.Equal(2000, version.TokensIn);
        Assert.Equal(1500, version.TokensOut);
    }

    // Scenario: Direct generation requires a non-empty prompt
    [Fact]
    public async Task Direct_generation_requires_a_non_empty_prompt()
    {
        // When I generate with an empty prompt
        var request = new GenerateArtifactRequest
        {
            Title = "Draft",
            Prompt = "",
            Model = "claude-opus-5",
        };

        // Then I see a validation error
        await Assert.ThrowsAsync<ArtifactValidationException>(() => _artifacts.Generate(_projectId, request));

        // And no artifact is created
        Assert.Empty(_artifacts.List(_projectId));

        // And no generation was attempted.
        Assert.Equal(0, _chat.AskCount);
    }

    // ----- Creation path 4 — create blank & edit -----

    // Scenario: Creating a blank artifact yields an empty first version
    [Fact]
    public void Creating_a_blank_artifact_yields_an_empty_first_version()
    {
        // When I create a blank "doc" artifact titled "Scratch Draft"
        var artifact = _artifacts.Create(_projectId, "doc", "Scratch Draft");

        // Then an artifact "Scratch Draft" exists
        var loaded = _artifacts.Get(artifact.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Scratch Draft", loaded!.Title);

        // And it has one version with empty content
        using var db = _store.CreateDbContext();
        var versions = db.ArtifactVersions.AsNoTracking().Where(v => v.ArtifactId == artifact.Id).ToList();
        Assert.Single(versions);
        Assert.Equal("", versions[0].Content);

        // And that version's created_by is "user"
        Assert.Equal("user", versions[0].CreatedBy);
    }

    // Scenario: Editing a blank artifact's content persists it
    [Fact]
    public void Editing_a_blank_artifacts_content_persists_it()
    {
        // Given a blank "doc" artifact
        var artifact = _artifacts.Create(_projectId, "doc", "Scratch");

        // When I set its content to "# Draft" and save
        _artifacts.SetContent(artifact.Id, "# Draft");

        // Then the current version content is "# Draft"
        Assert.Equal("# Draft", CurrentVersion(_artifacts.Get(artifact.Id)!).Content);
    }

    // ----- emit_artifact / update_artifact contract (§7.4) -----

    // Scenario: An emit_artifact tool call produces an artifact via the artifact service
    [Fact]
    public void An_emit_artifact_tool_call_produces_an_artifact_via_the_artifact_service()
    {
        // Given the model returns an emit_artifact call with type "table", title "Segment Sizes", CSV content
        const string csv = "segment,twh\nlfp,0.7\nnmc,0.5";
        var command = new ArtifactEmitCommand(_projectId, "Segment Sizes", "table", csv);

        // When the generation completes (the Agent SDK loop routes the tool call to the service)
        var result = _artifacts.EmitArtifact(command);

        // Then an artifact "Segment Sizes" of type "table" exists
        var loaded = _artifacts.Get(result.ArtifactId);
        Assert.NotNull(loaded);
        Assert.Equal("Segment Sizes", loaded!.Title);
        Assert.Equal("table", loaded.Type);

        // And its content was written through the artifact service (not a silent file overwrite)
        var version = CurrentVersion(loaded);
        Assert.Equal(csv, version.Content);
        Assert.Contains(_artifacts.List(_projectId), a => a.Id == loaded.Id);
    }

    // Scenario: emit_artifact with a missing required field is rejected
    [Fact]
    public void Emit_artifact_with_a_missing_required_field_is_rejected()
    {
        // Given the model returns an emit_artifact call with no title
        var command = new ArtifactEmitCommand(_projectId, Title: "", "table", "a,b");

        // When the generation completes
        // Then the call is rejected with a contract error
        Assert.Throws<ArtifactContractException>(() => _artifacts.EmitArtifact(command));

        // And no artifact is created
        Assert.Empty(_artifacts.List(_projectId));
    }

    // ----- Persistence & schema (§5) -----

    // Scenario: A created artifact and its version match the schema
    [Fact]
    [Trait("Category", "integration")]
    public void A_created_artifact_and_its_version_match_the_schema()
    {
        // When I create an artifact with one version
        var provenance = ArtifactProvenance.Claude(
            "claude-opus-5", "the prompt", new[] { _resourceA.Id }, tokensIn: 5, tokensOut: 7);
        var artifact = _artifacts.CreateFromContent(
            _projectId, "doc", "Schema Check", "body", contentFormat: null, provenance);

        // Then an Artifact row has id, project_id, title, type, current_version_id, created_at, updated_at
        using var db = _store.CreateDbContext();
        var row = db.Artifacts.AsNoTracking().Single(a => a.Id == artifact.Id);
        Assert.False(string.IsNullOrEmpty(row.Id));
        Assert.Equal(_projectId, row.ProjectId);
        Assert.Equal("Schema Check", row.Title);
        Assert.Equal("doc", row.Type);
        Assert.False(string.IsNullOrEmpty(row.CurrentVersionId));
        Assert.False(string.IsNullOrEmpty(row.CreatedAt));
        Assert.False(string.IsNullOrEmpty(row.UpdatedAt));

        // And an ArtifactVersion row has version_no 1, content, content_format, model, prompt,
        // resource_scope_json, created_by, created_at
        var version = db.ArtifactVersions.AsNoTracking().Single(v => v.ArtifactId == artifact.Id);
        Assert.Equal(1, version.VersionNo);
        Assert.Equal("body", version.Content);
        Assert.Equal("markdown", version.ContentFormat);
        Assert.Equal("claude-opus-5", version.Model);
        Assert.Equal("the prompt", version.Prompt);
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Equal("claude", version.CreatedBy);
        Assert.False(string.IsNullOrEmpty(version.CreatedAt));
    }

    // Scenario: current_version_id points at the artifact's only version on creation
    [Fact]
    public void Current_version_id_points_at_the_artifacts_only_version_on_creation()
    {
        // When I create an artifact
        var artifact = _artifacts.Create(_projectId, "doc", "One Version");

        // Then the Artifact's current_version_id equals its version_no 1 version id
        using var db = _store.CreateDbContext();
        var v1 = db.ArtifactVersions.AsNoTracking().Single(v => v.ArtifactId == artifact.Id && v.VersionNo == 1);
        var row = db.Artifacts.AsNoTracking().Single(a => a.Id == artifact.Id);
        Assert.Equal(v1.Id, row.CurrentVersionId);
    }

    // Scenario: Artifact content is full-text searchable
    [Fact]
    [Trait("Category", "integration")]
    public void Artifact_content_is_full_text_searchable()
    {
        // Given an artifact whose version content mentions "lithium iron phosphate"
        var artifact = _artifacts.CreateFromContent(
            _projectId, "doc", "Chemistry Note",
            "LFP is lithium iron phosphate, a durable cathode.",
            contentFormat: null, ArtifactProvenance.User());

        // When I search the project for "lithium iron phosphate"
        var hits = _search.SearchArtifacts(_projectId, "lithium iron phosphate");

        // Then the artifact appears in the results
        Assert.Contains(hits, h => h.Title == "Chemistry Note");
    }

    // ----- Management basics -----

    // Scenario: Renaming an artifact updates its title and timestamp
    [Fact]
    public void Renaming_an_artifact_updates_its_title_and_timestamp()
    {
        // Given an artifact "Draft A"
        var artifact = _artifacts.Create(_projectId, "doc", "Draft A");
        var before = _artifacts.Get(artifact.Id)!.UpdatedAt;

        // When I rename it to "Draft B"
        var renamed = _artifacts.Rename(artifact.Id, "Draft B");

        // Then the artifact is named "Draft B"
        Assert.Equal("Draft B", renamed.Title);
        Assert.Equal("Draft B", _artifacts.Get(artifact.Id)!.Title);

        // And its updated_at is newer than before
        Assert.True(string.CompareOrdinal(renamed.UpdatedAt, before) > 0);
    }
}

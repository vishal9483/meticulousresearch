using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Artifacts.Diff;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Artifacts;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> scenario in docs/features/edit-with-claude/tests.md
/// (SPEC §3.4, §5, §9.1(5)). None carry an excluded <c>Category</c> trait, so they run in the headless
/// gate over a real <see cref="EditWithClaudeService"/>, a real <see cref="ArtifactService"/> (the
/// artifact-versioning entry point), and a temp SQLite store. AI generation is served by the
/// deterministic <see cref="FakeChatService"/>; timestamps come from an injected clock
/// (TESTING-STRATEGY §4).
///
/// Background: an artifact "Market Research Report" with version 1 generated from a template; AI
/// generation served by a deterministic FakeChatService.
/// </summary>
public sealed class EditWithClaudeServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ArtifactService _artifacts;
    private readonly ArtifactDiffService _diff = new();
    private readonly EditWithClaudeService _edit;
    private readonly string _projectId;
    private readonly Artifact _artifact;

    public EditWithClaudeServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-edit-claude-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _artifacts = new ArtifactService(_store, _chat, _clock);
        _edit = new EditWithClaudeService(_artifacts, _chat, _projects, _resources, new StubCostCalculator());

        _projectId = _projects.Create("EV Batteries 2026").Id;
        // Version 1 generated from a template (created_by "claude").
        _artifact = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Research Report", "# Market Research Report\nv1 body",
            contentFormat: null,
            ArtifactProvenance.Claude("claude-opus-5", "seed prompt", Array.Empty<string>(), 10, 20, 0.01));
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

    private ArtifactVersion CurrentVersion(string artifactId)
    {
        var artifact = _artifacts.Get(artifactId)!;
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking().Single(v => v.Id == artifact.CurrentVersionId);
    }

    // Scenario: A follow-up instruction creates a new Claude-authored version
    [Fact]
    public async Task A_follow_up_instruction_creates_a_new_claude_authored_version()
    {
        _chat.WithCompletionText("# Market Research Report\nv2 body with 2031 forecast").WithUsage(50, 40);

        var v2 = await _edit.EditWithClaude(
            _artifact.Id, "Add a 2031 forecast row and tighten the summary", "claude-opus-5");

        var history = _artifacts.GetHistory(_artifact.Id);
        Assert.Equal(2, history.Count);
        var latest = history.Single(v => v.VersionNo == 2);
        Assert.Equal("# Market Research Report\nv2 body with 2031 forecast", latest.Content);
        Assert.Equal(ArtifactProvenance.CreatedByClaude, latest.CreatedBy);
        // Version 1 is unchanged.
        var v1 = history.Single(v => v.VersionNo == 1);
        Assert.Equal("# Market Research Report\nv1 body", v1.Content);
        Assert.Equal(2, v2.VersionNo);
    }

    // Scenario: The follow-up sees the current version as context
    [Fact]
    public async Task The_follow_up_sees_the_current_version_as_context()
    {
        // Make version 2 current so the edit targets it.
        _chat.WithCompletionText("v2 body content").WithUsage(1, 1);
        await _edit.EditWithClaude(_artifact.Id, "expand section", "claude-opus-5");

        _chat.WithCompletionText("v3 formal body").WithUsage(1, 1);
        await _edit.EditWithClaude(_artifact.Id, "make the tone more formal", "claude-opus-5");

        var sent = _chat.LastContext!;
        Assert.Contains("v2 body content", sent.UserMessage);
        Assert.Contains("make the tone more formal", sent.UserMessage);
    }

    // Scenario: The edit is grounded in the project's enabled resources
    [Fact]
    public async Task The_edit_is_grounded_in_the_projects_enabled_resources()
    {
        var enabledA = _resources.AddText(_projectId, "Cell chemistry", "NMC vs LFP tradeoffs.");
        var enabledB = _resources.AddText(_projectId, "Demand model", "EV demand grows through 2026.");
        var disabled = _resources.AddText(_projectId, "Old notes", "Outdated.");
        _resources.SetEnabled(disabled.Id, false);

        _chat.WithCompletionText("cited body").WithUsage(1, 1);
        var version = await _edit.EditWithClaude(
            _artifact.Id, "cite sources for the market size claim", "claude-opus-5");

        var scopeIds = _chat.LastContext!.Resources.Select(r => r.Id).ToList();
        Assert.Equal(2, scopeIds.Count);
        Assert.Contains(enabledA.Id, scopeIds);
        Assert.Contains(enabledB.Id, scopeIds);
        Assert.DoesNotContain(disabled.Id, scopeIds);

        // The new version's resource_scope_json records those 2 ids.
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(enabledA.Id, version.ResourceScopeJson!);
        Assert.Contains(enabledB.Id, version.ResourceScopeJson!);
        Assert.DoesNotContain(disabled.Id, version.ResourceScopeJson!);
    }

    // Scenario: A follow-up instruction is required
    [Fact]
    public async Task A_follow_up_instruction_is_required()
    {
        await Assert.ThrowsAsync<ArtifactValidationException>(
            () => _edit.EditWithClaude(_artifact.Id, "   ", "claude-opus-5"));

        // No new version is created.
        Assert.Single(_artifacts.GetHistory(_artifact.Id));
    }

    // Scenario: A Claude edit records model, prompt, and usage
    [Fact]
    public async Task A_claude_edit_records_model_prompt_and_usage()
    {
        _chat.WithCompletionText("revised body").WithUsage(1100, 700);

        var version = await _edit.EditWithClaude(
            _artifact.Id, "cite sources for the market size claim", "claude-opus-5");

        Assert.Equal("claude-opus-5", version.Model);
        Assert.Equal("cite sources for the market size claim", version.Prompt);
        Assert.Equal(1100, version.TokensIn);
        Assert.Equal(700, version.TokensOut);
        Assert.NotNull(version.CostUsd);
        Assert.True(version.CostUsd > 0);
    }

    // Scenario: The model can be chosen per edit
    [Fact]
    public async Task The_model_can_be_chosen_per_edit()
    {
        _chat.WithCompletionText("revised body").WithUsage(1, 1);

        var version = await _edit.EditWithClaude(_artifact.Id, "revise", "claude-sonnet-5");

        Assert.Equal("claude-sonnet-5", version.Model);
    }

    // Scenario: A manual edit creates a user-authored version
    [Fact]
    public void A_manual_edit_creates_a_user_authored_version()
    {
        var draft = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Draft doc", "# Draft", contentFormat: null, ArtifactProvenance.User());

        var version = _edit.SaveManualEdit(draft.Id, "# Final");

        Assert.NotNull(version);
        Assert.Equal(2, version!.VersionNo);
        Assert.Equal("# Final", version.Content);
        Assert.Equal(ArtifactProvenance.CreatedByUser, version.CreatedBy);
        Assert.Equal(0, version.TokensIn);
        Assert.Equal(0, version.TokensOut);
        Assert.True(version.CostUsd is null or 0);
    }

    // Scenario: Saving a manual edit with no changes does not create a version
    [Fact]
    public void Saving_a_manual_edit_with_no_changes_does_not_create_a_version()
    {
        var draft = _artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Draft doc", "# Draft", contentFormat: null, ArtifactProvenance.User());

        var version = _edit.SaveManualEdit(draft.Id, "# Draft");

        Assert.Null(version);
        Assert.Single(_artifacts.GetHistory(draft.Id));
    }

    // Scenario: An Edit-with-Claude generation streams into a preview before committing
    [Fact]
    public async Task An_edit_streams_into_a_preview_before_committing()
    {
        _chat.WithTokens("Hello ", "revised ", "world");

        var previews = new List<string>();
        var committedVersionCountDuringStream = -1;
        var preview = new Progress<string>(text =>
        {
            previews.Add(text);
            // A new version is committed only when the stream completes: while streaming, history
            // must still hold just version 1.
            if (committedVersionCountDuringStream < 0)
                committedVersionCountDuringStream = _artifacts.GetHistory(_artifact.Id).Count;
        });

        var version = await _edit.EditWithClaude(_artifact.Id, "revise the artifact", "claude-opus-5", preview);

        // The revised content streamed into the preview.
        Assert.NotEmpty(previews);
        Assert.Equal("Hello revised world", previews[^1]);
        // No version existed yet while the stream was still delivering tokens.
        Assert.Equal(1, committedVersionCountDuringStream);
        // A version is committed once the stream completes.
        Assert.Equal(2, version.VersionNo);
        Assert.Equal("Hello revised world", version.Content);
    }

    // Scenario: Cancelling an in-progress Claude edit creates no version
    [Fact]
    public async Task Cancelling_an_in_progress_claude_edit_creates_no_version()
    {
        _chat.WithTokens("part 1", "part 2", "part 3");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _edit.EditWithClaude(_artifact.Id, "revise", "claude-opus-5", preview: null, cts.Token));

        // No new version; the current version is unchanged.
        Assert.Single(_artifacts.GetHistory(_artifact.Id));
        Assert.Equal("# Market Research Report\nv1 body", CurrentVersion(_artifact.Id).Content);
    }

    // Scenario: A failed Claude edit surfaces an error and creates no version
    [Fact]
    public async Task A_failed_claude_edit_surfaces_an_error_and_creates_no_version()
    {
        _chat.FailWith(ChatErrorKind.ServerError, retryable: true, "The model service is temporarily unavailable.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _edit.EditWithClaude(_artifact.Id, "revise", "claude-opus-5"));
        Assert.Equal("The model service is temporarily unavailable.", ex.Message);

        // No new version created.
        Assert.Single(_artifacts.GetHistory(_artifact.Id));
    }

    // Scenario: Iterating a Market Research Report produces an ordered version chain
    [Fact]
    public async Task Iterating_a_market_research_report_produces_an_ordered_version_chain()
    {
        _chat.WithCompletionText("# Report\nline A\nline B v2").WithUsage(1, 1);
        var v2 = await _edit.EditWithClaude(_artifact.Id, "add section A", "claude-opus-5");

        _chat.WithCompletionText("# Report\nline A\nline B v3\nline C v3").WithUsage(1, 1);
        var v3 = await _edit.EditWithClaude(_artifact.Id, "add section C", "claude-opus-5");

        Assert.Equal(2, v2.VersionNo);
        Assert.Equal(3, v3.VersionNo);

        var history = _artifacts.GetHistory(_artifact.Id);
        Assert.Equal(new long[] { 3, 2, 1 }, history.Select(v => v.VersionNo).ToArray());

        // I can diff version 1 against version 3 to see the cumulative change.
        var v1 = history.Single(v => v.VersionNo == 1);
        var diff = _diff.Diff(v1, v3);
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.AddedSegments, s => s.Text.Contains("line C v3"));
    }

    /// <summary>Deterministic non-zero cost seam so Claude edits record a positive cost_usd (SPEC §3.6).</summary>
    private sealed class StubCostCalculator : ITurnCostCalculator
    {
        public TurnCostBreakdown Calculate(TurnMetadata metadata) =>
            new(metadata.InputTokens * 0.00001, metadata.OutputTokens * 0.00003, 0, 0);
    }
}

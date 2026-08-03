using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Turns;

/// <summary>
/// Faithful xUnit translation of the @unit "Turn metadata" scenarios in
/// docs/features/turn-metadata-actions/tests.md (SPEC §3.3, §5). These run in the headless gate:
/// they drive a real <see cref="ConversationService"/> over a temp SQLite store through the scripted
/// <see cref="FakeChatService"/>, then project <see cref="TurnMetadata"/> from the persisted turn.
/// </summary>
public sealed class TurnMetadataTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _service;

    public TurnMetadataTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-turn-metadata-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _service = new ConversationService(_store, _chat, _projects, _resources, _clock);
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
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

    // Scenario: A completed turn exposes model, token usage, latency, and resource scope
    [Fact]
    public async Task A_completed_turn_exposes_model_token_usage_latency_and_resource_scope()
    {
        var project = _projects.Create("P");

        // And resources "A" and "B" were in scope
        var a = _resources.AddText(project.Id, "A", "resource A body");
        var b = _resources.AddText(project.Id, "B", "resource B body");

        var conversation = _service.Create(project.Id);

        // Given a turn produced by "claude-sonnet-5" with usage in=900 out=200
        _chat.WithCompletionText("answer").WithUsage(900, 200);
        var assistant = await _service.Ask(conversation.Id, "q", "claude-sonnet-5");

        // When I inspect the turn's metadata
        var metadata = TurnMetadata.FromMessage(assistant);

        // Then it shows model "claude-sonnet-5"
        Assert.Equal("claude-sonnet-5", metadata.Model);

        // And input tokens 900 and output tokens 200
        Assert.Equal(900, metadata.InputTokens);
        Assert.Equal(200, metadata.OutputTokens);

        // And a latency value
        Assert.NotNull(metadata.LatencyMs);
        Assert.True(metadata.LatencyMs >= 0);

        // And resource scope "A", "B"
        Assert.Equal(2, metadata.ResourceScope.Count);
        Assert.Contains(a.Id, metadata.ResourceScope);
        Assert.Contains(b.Id, metadata.ResourceScope);
    }
}

using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Models;

/// <summary>
/// Faithful xUnit translation of the selection &amp; override and recording @unit scenarios in
/// docs/features/model-selector/tests.md (SPEC §3.3). Pure selection state is exercised through
/// <see cref="ModelSelection"/>; "the next turn is sent with model X" and "the assistant turn records
/// model X" are verified end-to-end through the <see cref="ConversationService"/> and the scripted
/// <see cref="FakeChatService"/> (which records the model on the assembled request).
/// </summary>
public sealed class ModelSelectionTests : IDisposable
{
    private readonly string _dataDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _service;

    public ModelSelectionTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-model-selection-tests", Guid.NewGuid().ToString("N"));
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

    // Scenario: A conversation inherits the project default model
    //   Given a project whose default model is "claude-opus-5"
    //   When I start a new conversation
    //   Then the conversation's selected model is "claude-opus-5"
    [Fact]
    public void A_conversation_inherits_the_project_default_model()
    {
        // Given a project whose default model is "claude-opus-5"
        var project = _projects.Create("P", defaultModel: "claude-opus-5");

        // When I start a new conversation
        var selection = ModelSelection.ForNewConversation(project.DefaultModel!);

        // Then the conversation's selected model is "claude-opus-5"
        Assert.Equal("claude-opus-5", selection.ConversationModelId);
    }

    // Scenario: Changing the conversation model applies to subsequent turns
    //   Given a conversation using "claude-opus-5"
    //   When I change the conversation model to "claude-sonnet-5"
    //   Then the next turn is sent with model "claude-sonnet-5"
    [Fact]
    public async Task Changing_the_conversation_model_applies_to_subsequent_turns()
    {
        // Given a conversation using "claude-opus-5"
        var project = _projects.Create("P", defaultModel: "claude-opus-5");
        var conversation = _service.Create(project.Id);
        var selection = ModelSelection.ForNewConversation(project.DefaultModel!);

        // When I change the conversation model to "claude-sonnet-5"
        selection.SetConversationModel("claude-sonnet-5");

        // Then the next turn is sent with model "claude-sonnet-5"
        _chat.WithCompletionText("ok").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "hello", selection.ResolveForTurn());

        Assert.NotNull(_chat.LastContext);
        Assert.Equal("claude-sonnet-5", _chat.LastContext!.Model);
    }

    // Scenario: A per-message override does not change the conversation default
    //   Given a conversation using "claude-sonnet-5"
    //   When I send one message overridden to "claude-haiku-4-5"
    //   Then that turn uses "claude-haiku-4-5"
    //   And the conversation default remains "claude-sonnet-5"
    [Fact]
    public async Task A_per_message_override_does_not_change_the_conversation_default()
    {
        // Given a conversation using "claude-sonnet-5"
        var project = _projects.Create("P", defaultModel: "claude-sonnet-5");
        var conversation = _service.Create(project.Id);
        var selection = ModelSelection.ForNewConversation(project.DefaultModel!);

        // When I send one message overridden to "claude-haiku-4-5"
        _chat.WithCompletionText("ok").WithUsage(1, 1);
        var turnModel = selection.ResolveForTurn("claude-haiku-4-5");
        await _service.Ask(conversation.Id, "hello", turnModel);

        // Then that turn uses "claude-haiku-4-5"
        Assert.Equal("claude-haiku-4-5", _chat.LastContext!.Model);

        // And the conversation default remains "claude-sonnet-5"
        Assert.Equal("claude-sonnet-5", selection.ConversationModelId);
    }

    // Scenario: The model used is recorded on the assistant turn
    //   Given a conversation using "claude-sonnet-5"
    //   When a turn completes
    //   Then the assistant message records model "claude-sonnet-5"
    [Fact]
    public async Task The_model_used_is_recorded_on_the_assistant_turn()
    {
        // Given a conversation using "claude-sonnet-5"
        var project = _projects.Create("P", defaultModel: "claude-sonnet-5");
        var conversation = _service.Create(project.Id);
        var selection = ModelSelection.ForNewConversation(project.DefaultModel!);

        // When a turn completes
        _chat.WithCompletionText("answer").WithUsage(3, 7);
        var assistant = await _service.Ask(conversation.Id, "question", selection.ResolveForTurn());

        // Then the assistant message records model "claude-sonnet-5"
        Assert.Equal("claude-sonnet-5", assistant.Model);
    }
}

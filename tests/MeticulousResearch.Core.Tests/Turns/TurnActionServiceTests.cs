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
/// Faithful xUnit translation of the @unit "Actions" scenarios in
/// docs/features/turn-metadata-actions/tests.md (SPEC §3.3) that operate on the conversation:
/// retry (same/other model), edit-and-resend, promote-to-artifact (request/provenance), and delete.
/// These run in the headless gate over a real <see cref="ConversationService"/> and temp SQLite
/// store, driven by the scripted <see cref="FakeChatService"/>. (Copy-to-clipboard is a view concern
/// tested in the App view-model tests.)
/// </summary>
public sealed class TurnActionServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _conversations;
    private readonly TurnActionService _actions;

    public TurnActionServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-turn-actions-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _conversations = new ConversationService(_store, _chat, _projects, _resources, _clock);
        _actions = new TurnActionService(_store, _conversations, _resources);
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

    private IReadOnlyList<Message> Assistants(string conversationId)
        => _conversations.GetMessages(conversationId).Where(m => m.Role == "assistant").ToList();

    // Scenario: Retry (same model) generates a fresh answer to the same question
    [Fact]
    public async Task Retry_same_model_generates_a_fresh_answer_to_the_same_question()
    {
        var project = _projects.Create("P");
        var conversation = _conversations.Create(project.Id);

        // Given a user question and its assistant answer using "claude-opus-5"
        _chat.WithCompletionText("first answer").WithUsage(1, 1);
        var original = await _conversations.Ask(conversation.Id, "What is the market size?", "claude-opus-5");

        // When I retry with the same model
        _chat.WithCompletionText("fresh answer").WithUsage(1, 1);
        var regenerated = await _actions.Retry(original.Id);

        // Then a new assistant turn is generated for the same question using "claude-opus-5"
        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal("claude-opus-5", regenerated.Model);

        var assistants = Assistants(conversation.Id);
        Assert.Single(assistants);
        Assert.Equal(regenerated.Id, assistants[0].Id);

        // And the same question is still the user turn preceding it.
        var messages = _conversations.GetMessages(conversation.Id);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "What is the market size?");
    }

    // Scenario: Retry with another model uses the chosen model
    [Fact]
    public async Task Retry_with_another_model_uses_the_chosen_model()
    {
        var project = _projects.Create("P");
        var conversation = _conversations.Create(project.Id);

        // Given an assistant answer produced by "claude-opus-5"
        _chat.WithCompletionText("opus answer").WithUsage(1, 1);
        var original = await _conversations.Ask(conversation.Id, "Summarize the landscape", "claude-opus-5");

        // When I retry with "claude-haiku-4-5"
        _chat.WithCompletionText("haiku answer").WithUsage(1, 1);
        var regenerated = await _actions.Retry(original.Id, modelOverride: "claude-haiku-4-5");

        // Then a new assistant turn is generated using "claude-haiku-4-5"
        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal("claude-haiku-4-5", regenerated.Model);
        var assistants = Assistants(conversation.Id);
        Assert.Single(assistants);
        Assert.Equal("claude-haiku-4-5", assistants[0].Model);
    }

    // Scenario: Edit-and-resend replaces the user message and regenerates
    [Fact]
    public async Task Edit_and_resend_replaces_the_user_message_and_regenerates()
    {
        var project = _projects.Create("P");
        var conversation = _conversations.Create(project.Id);

        // Given a user message "What is the CAGR?" with an assistant answer
        _chat.WithCompletionText("cagr answer").WithUsage(1, 1);
        var original = await _conversations.Ask(conversation.Id, "What is the CAGR?", "claude-opus-5");

        // When I edit it to "What is the 10-year CAGR?" and resend
        _chat.WithCompletionText("10-year cagr answer").WithUsage(1, 1);
        var regenerated = await _actions.EditAndResend(original.Id, "What is the 10-year CAGR?");

        // Then the user message becomes "What is the 10-year CAGR?"
        var messages = _conversations.GetMessages(conversation.Id);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "What is the 10-year CAGR?");
        Assert.DoesNotContain(messages, m => m.Role == "user" && m.Content == "What is the CAGR?");

        // And a new assistant turn is generated for the edited message
        Assert.NotEqual(original.Id, regenerated.Id);
        var assistants = Assistants(conversation.Id);
        Assert.Single(assistants);
        Assert.Equal(regenerated.Id, assistants[0].Id);
    }

    // Scenario: Promote to artifact creates an artifact from the assistant turn
    [Fact]
    public async Task Promote_to_artifact_creates_an_artifact_from_the_assistant_turn()
    {
        var project = _projects.Create("P");
        var a = _resources.AddText(project.Id, "A", "resource A body");
        var b = _resources.AddText(project.Id, "B", "resource B body");
        var conversation = _conversations.Create(project.Id);

        // Given a completed assistant turn
        _chat.WithCompletionText("The TAM is $12B").WithUsage(10, 5);
        var assistant = await _conversations.Ask(conversation.Id, "What is the TAM?", "claude-sonnet-5");

        // When I promote it to an artifact
        var request = _actions.BuildPromoteRequest(assistant.Id);

        // Then an artifact is created carrying the turn's content
        // (artifact domain is owned by M3; here we assert the promote request/provenance)
        Assert.Equal("The TAM is $12B", request.Content);

        // And the source turn, model, and resource scope are recorded as provenance
        Assert.Equal(assistant.Id, request.Provenance.SourceTurnId);
        Assert.Equal("claude-sonnet-5", request.Provenance.Model);
        Assert.Equal(2, request.Provenance.ResourceScope.Count);
        Assert.Contains(a.Id, request.Provenance.ResourceScope);
        Assert.Contains(b.Id, request.Provenance.ResourceScope);
    }

    // Scenario: Deleting a turn removes it from the conversation
    [Fact]
    public async Task Deleting_a_turn_removes_it_from_the_conversation()
    {
        var project = _projects.Create("P");
        var conversation = _conversations.Create(project.Id);

        // Given a conversation with an assistant turn
        _chat.WithCompletionText("an answer").WithUsage(1, 1);
        var assistant = await _conversations.Ask(conversation.Id, "a question", "claude-opus-5");
        Assert.Contains(_conversations.GetMessages(conversation.Id), m => m.Id == assistant.Id);

        // When I delete the turn
        _actions.Delete(assistant.Id);

        // Then the turn no longer appears in the conversation
        Assert.DoesNotContain(_conversations.GetMessages(conversation.Id), m => m.Id == assistant.Id);
    }
}

using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Time;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Conversations;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/conversations/tests.md
/// (SPEC §3.3, §5, §7.3). These are @unit and run in the headless gate; they touch a temp SQLite
/// database (TESTING-STRATEGY §4) and drive generation through the scripted <see cref="FakeChatService"/>.
/// </summary>
public sealed class ConversationServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-conversations-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _service = new ConversationService(_store, _chat, _projects, _resources, _clock);
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

    // ---------------------------------------------------------------- Scope

    // Scenario: A conversation belongs to exactly one project
    [Fact]
    public void A_conversation_belongs_to_exactly_one_project()
    {
        // Given a project "Semiconductors 2026"
        var project = _projects.Create("Semiconductors 2026");

        // When I create a conversation in it
        var conversation = _service.Create(project.Id);

        // Then the conversation's project is "Semiconductors 2026"
        Assert.Equal(project.Id, conversation.ProjectId);
        Assert.Equal("Semiconductors 2026", _projects.Get(conversation.ProjectId)!.Name);
    }

    // Scenario: Conversations from other projects are not listed here
    [Fact]
    public void Conversations_from_other_projects_are_not_listed_here()
    {
        // Given a project "A" with 2 conversations
        var a = _projects.Create("A");
        var a1 = _service.Create(a.Id);
        var a2 = _service.Create(a.Id);

        // And a project "B" with 1 conversation
        var b = _projects.Create("B");
        _service.Create(b.Id);

        // When I list conversations for project "A"
        var listed = _service.List(a.Id);

        // Then I see exactly the 2 conversations from project "A"
        Assert.Equal(2, listed.Count);
        Assert.All(listed, c => Assert.Equal(a.Id, c.ProjectId));
        Assert.Contains(listed, c => c.Id == a1.Id);
        Assert.Contains(listed, c => c.Id == a2.Id);
    }

    // Scenario: Deleting a conversation removes its messages
    [Fact]
    public async Task Deleting_a_conversation_removes_its_messages()
    {
        // Given a conversation with 4 messages (two completed turns → user+assistant ×2)
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("reply-1").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "q1", "claude-opus-5");
        _chat.WithCompletionText("reply-2").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "q2", "claude-opus-5");
        Assert.Equal(4, _service.GetMessages(conversation.Id).Count);

        // When I delete the conversation
        _service.Delete(conversation.Id);

        // Then the conversation no longer exists
        Assert.Null(_service.Get(conversation.Id));

        // And its messages no longer exist
        Assert.Empty(_service.GetMessages(conversation.Id));
    }

    // ---------------------------------------------------------------- Grounding (§7.3)

    // Scenario: The request is grounded in custom instructions, enabled resources, history, and the message
    [Fact]
    public async Task The_request_is_grounded_in_instructions_resources_history_and_the_message()
    {
        // Given a project with custom instructions "Formal tone; cite sources"
        var project = _projects.Create("P", customInstructions: "Formal tone; cite sources");

        // And enabled resources "Filing.pdf" and "Interview.txt"
        _resources.AddText(project.Id, "Filing.pdf", "Filing extracted body");
        _resources.AddText(project.Id, "Interview.txt", "Interview extracted body");

        // And a conversation with one prior user/assistant turn
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("Prior answer").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "Prior question", "claude-opus-5");

        // When I ask "Summarize the competitive landscape"
        _chat.WithCompletionText("Answer").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "Summarize the competitive landscape", "claude-opus-5");

        var ctx = _chat.LastContext!;

        // Then the grounded request includes the custom instructions as system context
        Assert.Equal("Formal tone; cite sources", ctx.CustomInstructions);

        // And the extracted text of "Filing.pdf" and "Interview.txt"
        Assert.Contains(ctx.Resources, r => r.Title == "Filing.pdf" && r.Text == "Filing extracted body");
        Assert.Contains(ctx.Resources, r => r.Title == "Interview.txt" && r.Text == "Interview extracted body");

        // And the prior turn
        Assert.Contains(ctx.History, h => h.Role == "user" && h.Content == "Prior question");
        Assert.Contains(ctx.History, h => h.Role == "assistant" && h.Content == "Prior answer");

        // And the new user message
        Assert.Equal("Summarize the competitive landscape", ctx.UserMessage);
    }

    // Scenario: Disabled resources are excluded from grounding
    [Fact]
    public async Task Disabled_resources_are_excluded_from_grounding()
    {
        // Given a project with an enabled resource "A" and a disabled resource "B"
        var project = _projects.Create("P");
        _resources.AddText(project.Id, "A", "A body");
        var b = _resources.AddText(project.Id, "B", "B body");
        _resources.SetEnabled(b.Id, false);

        var conversation = _service.Create(project.Id);

        // When I ask a question
        _chat.WithCompletionText("Answer").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "A question", "claude-opus-5");

        var ctx = _chat.LastContext!;

        // Then the grounded request includes "A"
        Assert.Contains(ctx.Resources, r => r.Title == "A");

        // And does not include "B"
        Assert.DoesNotContain(ctx.Resources, r => r.Title == "B");
    }

    // Scenario: The resource scope used is recorded on the assistant turn
    [Fact]
    public async Task The_resource_scope_used_is_recorded_on_the_assistant_turn()
    {
        // Given enabled resources "A" and "B" in scope
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        var scope = new List<ChatResource>
        {
            new("A", "A", "A body"),
            new("B", "B", "B body"),
        };

        // When a turn completes
        _chat.WithCompletionText("Answer").WithUsage(1, 1);
        var assistant = await _service.Ask(conversation.Id, "A question", "claude-opus-5", scope);

        // Then the assistant message records resource_scope containing "A" and "B"
        Assert.NotNull(assistant.ResourceScopeJson);
        var recorded = System.Text.Json.JsonSerializer.Deserialize<string[]>(assistant.ResourceScopeJson!)!;
        Assert.Contains("A", recorded);
        Assert.Contains("B", recorded);
    }

    // Scenario: History is sent in order
    [Fact]
    public async Task History_is_sent_in_order()
    {
        // Given a conversation with turns T1 then T2
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        _chat.WithCompletionText("A1").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "T1", "claude-opus-5");
        _chat.WithCompletionText("A2").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "T2", "claude-opus-5");

        // When I ask a third question
        _chat.WithCompletionText("A3").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "T3", "claude-opus-5");

        var history = _chat.LastContext!.History;

        // Then the grounded request contains T1 before T2 before the new message
        var t1Index = IndexOfContent(history, "T1");
        var t2Index = IndexOfContent(history, "T2");
        Assert.True(t1Index >= 0, "T1 missing from history");
        Assert.True(t2Index >= 0, "T2 missing from history");
        Assert.True(t1Index < t2Index, "T1 must precede T2 in history");
        // The new message is not part of history; it is the separate user message.
        Assert.DoesNotContain(history, h => h.Content == "T3");
        Assert.Equal("T3", _chat.LastContext!.UserMessage);
    }

    // ---------------------------------------------------------------- Ask flow & persistence (§5)

    // Scenario: Asking a question persists a user message then an assistant message
    [Fact]
    public async Task Asking_a_question_persists_a_user_message_then_an_assistant_message()
    {
        // Given an empty conversation
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        Assert.Empty(_service.GetMessages(conversation.Id));

        // When I ask "What is the TAM?" and the backend completes with "The TAM is ..."
        _chat.WithCompletionText("The TAM is ...").WithUsage(10, 10);
        await _service.Ask(conversation.Id, "What is the TAM?", "claude-opus-5");

        var messages = _service.GetMessages(conversation.Id);

        // Then a user message "What is the TAM?" is persisted
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "What is the TAM?");

        // And an assistant message "The TAM is ..." is persisted
        Assert.Contains(messages, m => m.Role == "assistant" && m.Content == "The TAM is ...");

        // And the assistant message follows the user message
        var userIndex = messages.ToList().FindIndex(m => m.Role == "user");
        var assistantIndex = messages.ToList().FindIndex(m => m.Role == "assistant");
        Assert.True(userIndex >= 0 && assistantIndex >= 0);
        Assert.True(userIndex < assistantIndex);
    }

    // Scenario: The completed assistant turn persists usage, model, and latency
    [Fact]
    public async Task The_completed_assistant_turn_persists_usage_model_and_latency()
    {
        // Given a conversation using "claude-sonnet-5"
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);

        // And a backend that reports usage in=900 out=200 and completes after a measured interval
        _chat.WithCompletionText("Answer").WithUsage(900, 200);

        // When a turn completes
        var assistant = await _service.Ask(conversation.Id, "A question", "claude-sonnet-5");

        // Then the assistant message records model "claude-sonnet-5"
        Assert.Equal("claude-sonnet-5", assistant.Model);

        // And tokens_in 900 and tokens_out 200
        Assert.Equal(900, assistant.TokensIn);
        Assert.Equal(200, assistant.TokensOut);

        // And a latency_ms value greater than 0
        Assert.NotNull(assistant.LatencyMs);
        Assert.True(assistant.LatencyMs > 0, $"expected latency > 0 but was {assistant.LatencyMs}");
    }

    // Scenario: A conversation's updated_at advances when a turn completes
    [Fact]
    public async Task A_conversations_updated_at_advances_when_a_turn_completes()
    {
        // Given a conversation created at time T0
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        var t0 = conversation.UpdatedAt;

        // When a turn completes at a later time
        _chat.WithCompletionText("Answer").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "A question", "claude-opus-5");

        // Then the conversation's updated_at is newer than T0
        var refreshed = _service.Get(conversation.Id)!;
        Assert.True(
            string.CompareOrdinal(refreshed.UpdatedAt, t0) > 0,
            $"expected updated_at '{refreshed.UpdatedAt}' to be newer than T0 '{t0}'");
    }

    // Scenario: A new conversation gets a title
    [Fact]
    public async Task A_new_conversation_gets_a_title()
    {
        // Given an empty conversation with no title
        var project = _projects.Create("P");
        var conversation = _service.Create(project.Id);
        Assert.True(string.IsNullOrEmpty(conversation.Title));

        // When the first turn completes
        _chat.WithCompletionText("Answer").WithUsage(1, 1);
        await _service.Ask(conversation.Id, "What is the market size?", "claude-opus-5");

        // Then the conversation has a non-empty title
        var refreshed = _service.Get(conversation.Id)!;
        Assert.False(string.IsNullOrWhiteSpace(refreshed.Title));
    }

    private static int IndexOfContent(IReadOnlyList<ChatHistoryMessage> history, string content)
    {
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].Content == content)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// A monotonic <see cref="IClock"/> that advances by a fixed step on every read, so message
    /// timestamps strictly increase and a measured latency is positive without wall-clock flakiness.
    /// </summary>
    private sealed class AdvancingClock : IClock
    {
        private DateTimeOffset _now;
        private readonly TimeSpan _step;

        public AdvancingClock(DateTimeOffset start, TimeSpan step)
        {
            _now = start;
            _step = step;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                var value = _now;
                _now += _step;
                return value;
            }
        }
    }
}

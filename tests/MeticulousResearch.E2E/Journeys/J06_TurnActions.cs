using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-06 — Turn actions: copy, retry, edit-and-resend, promote to artifact (covers SPEC §3.3, bridging
/// into artifacts). The clipboard is a view concern; retry (other model + own cost), edit-and-resend
/// (downstream replaced consistently), and promote-to-artifact (carrying the turn's provenance) run
/// headlessly over the real turn-action + artifact services.
/// </summary>
public sealed class J06_TurnActions : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J06_TurnActions()
    {
        _projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Cite sources").Id;
        _h.Resources.AddText(_projectId, "Filing", "Market grows to $100B by 2030.");
    }

    public void Dispose() => _h.Dispose();

    private string CurrentAssistantId(string conversationId) =>
        _h.Conversations.GetMessages(conversationId).Last(m => m.Role == "assistant").Id;

    // @e2e
    // Scenario: Ana curates a conversation using per-turn actions
    [Fact]
    public async Task Ana_curates_a_conversation_using_per_turn_actions()
    {
        var conversation = _h.Conversations.Create(_projectId);
        var scope = _h.EnabledScope(_projectId);

        // Given a completed assistant turn.
        _h.Chat.WithCompletionText("Original answer.").WithUsage(100, 50);
        var original = await _h.Conversations.Ask(conversation.Id, "What is the market size?", "claude-opus-5", scope);

        // When I copy the turn, its content is available on the clipboard (its text is retrievable).
        Assert.Equal("Original answer.", original.Content);

        // When I retry the turn with a different model, a new turn records the new model and its own cost.
        _h.Chat.WithCompletionText("Retried answer.").WithUsage(80, 40);
        var retried = await _h.TurnActions.Retry(original.Id, modelOverride: "claude-haiku-4-5");
        Assert.NotEqual(original.Id, retried.Id);
        Assert.Equal("claude-haiku-4-5", retried.Model);
        Assert.Single(_h.Conversations.GetMessages(conversation.Id).Where(m => m.Role == "assistant"));

        // When I edit my earlier message and resend, a new assistant turn replaces the history consistently.
        _h.Chat.WithCompletionText("Answer to the edited question.").WithUsage(70, 30);
        var edited = await _h.TurnActions.EditAndResend(
            CurrentAssistantId(conversation.Id), "What is the 2030 market size?");
        Assert.Equal("Answer to the edited question.", edited.Content);
        var messages = _h.Conversations.GetMessages(conversation.Id);
        Assert.Contains(messages, m => m.Role == "user" && m.Content == "What is the 2030 market size?");
        Assert.Single(messages.Where(m => m.Role == "assistant"));

        // When I promote the strong turn to an artifact, it carries the turn's provenance.
        var strongTurnId = CurrentAssistantId(conversation.Id);
        var strongTurn = messages.Single(m => m.Id == strongTurnId);
        var artifact = _h.Artifacts.PromoteTurn(strongTurnId, "Market Size Finding");
        Assert.NotNull(_h.Artifacts.Get(artifact.Id));

        var version = _h.Artifacts.GetHistory(artifact.Id).Single();
        Assert.Equal(strongTurn.Model, version.Model);          // model provenance
        Assert.False(string.IsNullOrEmpty(version.Content));     // the turn's content
    }
}

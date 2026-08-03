using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// <see cref="IStreamingConversationService"/> over the <see cref="DataStore"/> (SPEC §3.3, §8).
/// Consumes the <see cref="IChatService"/> token stream owned by <c>ai-gateway</c>, appending each
/// delta to a live <see cref="StreamingTurn"/>, and persists the assistant turn on every terminal
/// outcome: the final text on <see cref="ChatCompleted"/>, or the accumulated partial marked
/// interrupted on <see cref="ChatCancelled"/> (user stop) or <see cref="ChatFaulted"/> (backend
/// fault). Grounding is assembled by <see cref="ConversationGroundingAssembler"/> exactly as the
/// non-streaming Ask flow does; timestamps come from the injected <see cref="IClock"/>.
/// </summary>
public sealed class StreamingConversationService : IStreamingConversationService
{
    private readonly DataStore _store;
    private readonly IChatService _chat;
    private readonly IProjectService _projects;
    private readonly IResourceService _resources;
    private readonly IClock _clock;
    private readonly ConversationGroundingAssembler _assembler;

    /// <summary>Creates the streaming service over its collaborators.</summary>
    public StreamingConversationService(
        DataStore store,
        IChatService chat,
        IProjectService projects,
        IResourceService resources,
        IClock clock)
        : this(store, chat, projects, resources, clock, new ConversationGroundingAssembler())
    {
    }

    /// <summary>Creates the streaming service with an explicit grounding assembler (testing seam).</summary>
    public StreamingConversationService(
        DataStore store,
        IChatService chat,
        IProjectService projects,
        IResourceService resources,
        IClock clock,
        ConversationGroundingAssembler assembler)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
    }

    /// <inheritdoc />
    public async Task<StreamingTurn> StreamAsk(
        string conversationId,
        string message,
        string model,
        Action<StreamingTurn>? onDelta = null,
        IReadOnlyList<ChatResource>? resourceScope = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("A model id is required.", nameof(model));
        ArgumentNullException.ThrowIfNull(message);

        var conversation = GetConversation(conversationId)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        var project = _projects.Get(conversation.ProjectId)
            ?? throw new InvalidOperationException($"Project '{conversation.ProjectId}' not found.");

        var scope = resourceScope ?? BuildEnabledScope(conversation.ProjectId);
        var history = BuildHistory(conversationId);

        // Persist the user turn up-front so it is durable even if generation is interrupted.
        PersistMessage(new Message
        {
            Id = NewId(),
            ConversationId = conversationId,
            Role = ConversationService.UserRole,
            Content = message,
            CreatedAt = Now(),
        });

        var context = _assembler.Assemble(project.CustomInstructions, model, message, scope, history);

        var turn = new StreamingTurn(conversationId, model);
        var accumulated = new StringBuilder();
        var start = _clock.UtcNow;

        var (completed, fault, cancelled) =
            await Consume(context, turn, accumulated, onDelta, cancellationToken).ConfigureAwait(false);

        var latencyMs = (long)Math.Max(0, (_clock.UtcNow - start).TotalMilliseconds);

        if (completed is not null)
        {
            turn.Text = completed.Text;
            turn.State = StreamingState.Completed;
            turn.PersistedMessageId = PersistAssistant(conversationId, model, completed.Text, completed.Usage, latencyMs, scope);
        }
        else
        {
            // Stop (cancelled) or backend fault: persist the accumulated partial, marked interrupted.
            turn.Text = accumulated.ToString();
            turn.State = StreamingState.Interrupted;
            turn.Fault = fault;
            turn.PersistedMessageId = PersistAssistant(conversationId, model, turn.Text, ChatUsage.Zero, latencyMs, scope);
            _ = cancelled; // both stop and fault land here; fault carries the retryable classification.
        }

        return turn;
    }

    /// <inheritdoc />
    public async Task<StreamingTurn> Resume(
        StreamingTurn turn,
        Action<StreamingTurn>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(turn);
        if (!turn.IsInterrupted)
            throw new InvalidOperationException("Only an interrupted turn can be resumed.");

        var conversation = GetConversation(turn.ConversationId)
            ?? throw new InvalidOperationException($"Conversation '{turn.ConversationId}' not found.");
        var project = _projects.Get(conversation.ProjectId)
            ?? throw new InvalidOperationException($"Project '{conversation.ProjectId}' not found.");

        var scope = BuildEnabledScope(conversation.ProjectId);
        // Re-issue generation with the existing partial answer as trailing context so the model
        // continues where it left off (M2 resume strategy; a provider-side resumable token is future).
        var history = BuildHistory(turn.ConversationId);
        var withPartial = new List<ChatHistoryMessage>(history)
        {
            new(ConversationService.AssistantRole, turn.Text),
        };
        var context = _assembler.Assemble(
            project.CustomInstructions, turn.Model, ContinuePrompt, scope, withPartial);

        turn.State = StreamingState.Streaming;
        turn.Fault = null;

        var accumulated = new StringBuilder(turn.Text);
        var start = _clock.UtcNow;

        var (completed, fault, _) =
            await Consume(context, turn, accumulated, onDelta, cancellationToken).ConfigureAwait(false);

        var latencyMs = (long)Math.Max(0, (_clock.UtcNow - start).TotalMilliseconds);

        if (completed is not null)
        {
            // The continued text is the partial plus the newly appended tokens.
            turn.Text = accumulated.ToString();
            turn.State = StreamingState.Completed;
            UpdateAssistantContent(turn.PersistedMessageId, turn.Text, completed.Usage, latencyMs);
        }
        else
        {
            turn.Text = accumulated.ToString();
            turn.State = StreamingState.Interrupted;
            turn.Fault = fault;
            UpdateAssistantContent(turn.PersistedMessageId, turn.Text, ChatUsage.Zero, latencyMs);
        }

        return turn;
    }

    /// <summary>The directive sent to continue an interrupted turn from its partial answer.</summary>
    private const string ContinuePrompt = "Continue the previous answer from where it stopped.";

    private async Task<(ChatCompleted? Completed, ChatFaulted? Fault, bool Cancelled)> Consume(
        ChatAskContext context,
        StreamingTurn turn,
        StringBuilder accumulated,
        Action<StreamingTurn>? onDelta,
        CancellationToken cancellationToken)
    {
        ChatCompleted? completed = null;
        ChatFaulted? fault = null;
        var cancelled = false;

        await foreach (var evt in _chat.Ask(context, cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ChatTokenDelta delta:
                    accumulated.Append(delta.Text);
                    turn.Text = accumulated.ToString();
                    onDelta?.Invoke(turn);
                    break;
                case ChatCompleted done:
                    completed = done;
                    break;
                case ChatCancelled:
                    cancelled = true;
                    break;
                case ChatFaulted f:
                    fault = f;
                    break;
            }
        }

        return (completed, fault, cancelled);
    }

    private Conversation? GetConversation(string conversationId)
    {
        using var db = _store.CreateDbContext();
        return db.Conversations.AsNoTracking().FirstOrDefault(c => c.Id == conversationId);
    }

    private IReadOnlyList<ChatHistoryMessage> BuildHistory(string conversationId)
    {
        using var db = _store.CreateDbContext();
        return db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .ToList()
            .OrderBy(m => m.CreatedAt, StringComparer.Ordinal)
            .Select(m => new ChatHistoryMessage(m.Role, m.Content))
            .ToList();
    }

    private IReadOnlyList<ChatResource> BuildEnabledScope(string projectId)
    {
        return _resources.ListEnabled(projectId)
            .Select(r => new ChatResource(r.Id, r.Title, _resources.GetExtractedText(r.Id)))
            .ToList();
    }

    private void PersistMessage(Message message)
    {
        using var db = _store.CreateDbContext();
        db.Messages.Add(message);
        db.SaveChanges();
    }

    private string PersistAssistant(
        string conversationId,
        string model,
        string content,
        ChatUsage usage,
        long latencyMs,
        IReadOnlyList<ChatResource> scope)
    {
        var assistant = new Message
        {
            Id = NewId(),
            ConversationId = conversationId,
            Role = ConversationService.AssistantRole,
            Content = content,
            Model = model,
            TokensIn = usage.InputTokens,
            TokensOut = usage.OutputTokens,
            TokensCacheRead = usage.CacheReadTokens,
            TokensCacheWrite = usage.CacheWriteTokens,
            CostUsd = null,
            LatencyMs = latencyMs,
            ResourceScopeJson = SerializeScope(scope),
            CreatedAt = Now(),
        };

        using var db = _store.CreateDbContext();
        db.Messages.Add(assistant);
        var tracked = db.Conversations.FirstOrDefault(c => c.Id == conversationId);
        if (tracked is not null)
            tracked.UpdatedAt = Now();
        db.SaveChanges();
        return assistant.Id;
    }

    private void UpdateAssistantContent(string? messageId, string content, ChatUsage usage, long latencyMs)
    {
        if (messageId is null)
            return;

        using var db = _store.CreateDbContext();
        var message = db.Messages.FirstOrDefault(m => m.Id == messageId);
        if (message is null)
            return;

        message.Content = content;
        message.TokensIn = usage.InputTokens;
        message.TokensOut = usage.OutputTokens;
        message.TokensCacheRead = usage.CacheReadTokens;
        message.TokensCacheWrite = usage.CacheWriteTokens;
        message.LatencyMs = latencyMs;

        var tracked = db.Conversations.FirstOrDefault(c => c.Id == message.ConversationId);
        if (tracked is not null)
            tracked.UpdatedAt = Now();

        db.SaveChanges();
    }

    private static string SerializeScope(IReadOnlyList<ChatResource> scope)
        => JsonSerializer.Serialize(scope.Select(r => r.Id).ToArray());

    private string Now() => _clock.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static string NewId() => Guid.NewGuid().ToString("N");
}

using System.Globalization;
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
/// <see cref="IConversationService"/> over the <see cref="DataStore"/> (SPEC §3.3, §5, §7.3).
/// Follows the repository pattern used across Core: short-lived <see cref="AppDbContext"/> instances,
/// timestamps from an injected <see cref="IClock"/>, and generation driven through the
/// <see cref="IChatService"/> gateway. Grounding is assembled once by
/// <see cref="ConversationGroundingAssembler"/>; token/model/latency and the recorded resource scope
/// are snapshotted onto the assistant <see cref="Message"/> at turn completion.
/// </summary>
public sealed class ConversationService : IConversationService
{
    /// <summary>Role value for user turns.</summary>
    public const string UserRole = "user";

    /// <summary>Role value for assistant turns.</summary>
    public const string AssistantRole = "assistant";

    /// <summary>Maximum length of a title auto-derived from the first user message.</summary>
    private const int TitleMaxLength = 80;

    private readonly DataStore _store;
    private readonly IChatService _chat;
    private readonly IProjectService _projects;
    private readonly IResourceService _resources;
    private readonly IClock _clock;
    private readonly ConversationGroundingAssembler _assembler;

    /// <summary>Creates the conversation service over its collaborators.</summary>
    public ConversationService(
        DataStore store,
        IChatService chat,
        IProjectService projects,
        IResourceService resources,
        IClock clock)
        : this(store, chat, projects, resources, clock, new ConversationGroundingAssembler())
    {
    }

    /// <summary>Creates the conversation service with an explicit grounding assembler (testing seam).</summary>
    public ConversationService(
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
    public Conversation Create(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));

        var now = Now();
        var conversation = new Conversation
        {
            Id = NewId(),
            ProjectId = projectId,
            Title = "",
            ModelDefault = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        using var db = _store.CreateDbContext();
        db.Conversations.Add(conversation);
        db.SaveChanges();
        return conversation;
    }

    /// <inheritdoc />
    public IReadOnlyList<Conversation> List(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Conversations.AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .ToList()
            .OrderByDescending(c => c.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public Conversation? Get(string conversationId)
    {
        using var db = _store.CreateDbContext();
        return db.Conversations.AsNoTracking().FirstOrDefault(c => c.Id == conversationId);
    }

    /// <inheritdoc />
    public void Delete(string conversationId)
    {
        using var db = _store.CreateDbContext();
        var conversation = db.Conversations.FirstOrDefault(c => c.Id == conversationId);
        if (conversation is null)
            return;

        // FK ON DELETE CASCADE (with PRAGMA foreign_keys=ON) removes the child Message rows.
        db.Conversations.Remove(conversation);
        db.SaveChanges();
    }

    /// <inheritdoc />
    public IReadOnlyList<Message> GetMessages(string conversationId)
    {
        using var db = _store.CreateDbContext();
        return db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .ToList()
            .OrderBy(m => m.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Message> Ask(
        string conversationId,
        string message,
        string model,
        IReadOnlyList<ChatResource>? resourceScope = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("A model id is required.", nameof(model));
        ArgumentNullException.ThrowIfNull(message);

        var conversation = Get(conversationId)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        var project = _projects.Get(conversation.ProjectId)
            ?? throw new InvalidOperationException($"Project '{conversation.ProjectId}' not found.");

        var scope = resourceScope ?? BuildEnabledScope(conversation.ProjectId);
        var history = BuildHistory(conversationId);

        // Persist the user turn before invoking the backend so it is durable even if generation fails.
        var userMessage = new Message
        {
            Id = NewId(),
            ConversationId = conversationId,
            Role = UserRole,
            Content = message,
            CreatedAt = Now(),
        };
        using (var db = _store.CreateDbContext())
        {
            db.Messages.Add(userMessage);
            db.SaveChanges();
        }

        var context = _assembler.Assemble(project.CustomInstructions, model, message, scope, history);

        var start = _clock.UtcNow;
        var text = new System.Text.StringBuilder();
        ChatCompleted? completed = null;
        ChatFaulted? faulted = null;
        var cancelled = false;

        await foreach (var evt in _chat.Ask(context, cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ChatTokenDelta delta:
                    text.Append(delta.Text);
                    break;
                case ChatCompleted done:
                    completed = done;
                    break;
                case ChatCancelled:
                    cancelled = true;
                    break;
                case ChatFaulted fault:
                    faulted = fault;
                    break;
            }
        }

        var end = _clock.UtcNow;

        if (faulted is not null)
            throw new InvalidOperationException(faulted.Message);
        if (cancelled || completed is null)
            throw new OperationCanceledException("The turn was cancelled before completion.");

        var latencyMs = (long)Math.Max(0, (end - start).TotalMilliseconds);
        var usage = completed.Usage;

        var assistantMessage = new Message
        {
            Id = NewId(),
            ConversationId = conversationId,
            Role = AssistantRole,
            Content = completed.Text,
            Model = model,
            TokensIn = usage.InputTokens,
            TokensOut = usage.OutputTokens,
            TokensCacheRead = usage.CacheReadTokens,
            TokensCacheWrite = usage.CacheWriteTokens,
            CostUsd = null, // Snapshot filled by cost-tracking later (§3.6).
            LatencyMs = latencyMs,
            ResourceScopeJson = SerializeScope(scope),
            CreatedAt = Now(),
        };

        using (var db = _store.CreateDbContext())
        {
            db.Messages.Add(assistantMessage);

            var tracked = db.Conversations.FirstOrDefault(c => c.Id == conversationId);
            if (tracked is not null)
            {
                tracked.UpdatedAt = Now();
                if (string.IsNullOrWhiteSpace(tracked.Title))
                    tracked.Title = DeriveTitle(message);
            }

            db.SaveChanges();
        }

        return assistantMessage;
    }

    private IReadOnlyList<ChatResource> BuildEnabledScope(string projectId)
    {
        return _resources.ListEnabled(projectId)
            .Select(r => new ChatResource(r.Id, r.Title, _resources.GetExtractedText(r.Id)))
            .ToList();
    }

    private IReadOnlyList<ChatHistoryMessage> BuildHistory(string conversationId)
    {
        return GetMessages(conversationId)
            .Select(m => new ChatHistoryMessage(m.Role, m.Content))
            .ToList();
    }

    private static string SerializeScope(IReadOnlyList<ChatResource> scope)
        => JsonSerializer.Serialize(scope.Select(r => r.Id).ToArray());

    private static string DeriveTitle(string firstUserMessage)
    {
        var trimmed = (firstUserMessage ?? "").Trim();
        if (trimmed.Length == 0)
            return "New conversation";

        return trimmed.Length <= TitleMaxLength ? trimmed : trimmed[..TitleMaxLength].TrimEnd() + "…";
    }

    private string Now() => _clock.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static string NewId() => Guid.NewGuid().ToString("N");
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.Core.Turns;

/// <summary>
/// <see cref="ITurnActionService"/> over the <see cref="DataStore"/> (SPEC §3.3). Retry and
/// edit-and-resend supersede the old user/assistant pair and re-drive generation through the
/// <see cref="IConversationService"/> Ask path — preserving the original turn's in-scope resources
/// (reconstructed from the persisted resource ids via <see cref="IResourceService"/>) and honouring
/// the optional per-message model override. Promote-to-artifact assembles the request/provenance
/// consumed by <c>artifact-creation</c> (M3). Delete removes a single turn row.
/// </summary>
public sealed class TurnActionService : ITurnActionService
{
    private const string UserRole = "user";

    private readonly DataStore _store;
    private readonly IConversationService _conversations;
    private readonly IResourceService _resources;

    /// <summary>Creates the turn-action service over its collaborators.</summary>
    /// <param name="store">The data store holding message rows.</param>
    /// <param name="conversations">The conversation service driving regeneration.</param>
    /// <param name="resources">The resource service used to rebuild a turn's in-scope resources.</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    public TurnActionService(
        DataStore store,
        IConversationService conversations,
        IResourceService resources)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <inheritdoc />
    public TurnMetadata GetMetadata(string messageId)
        => TurnMetadata.FromMessage(RequireMessage(messageId));

    /// <inheritdoc />
    public Task<Message> Retry(
        string assistantMessageId,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var assistant = RequireMessage(assistantMessageId);
        var user = RequirePrecedingUser(assistant);
        return Regenerate(assistant, user, user.Content, modelOverride, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> EditAndResend(
        string assistantMessageId,
        string newUserMessage,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newUserMessage))
            throw new ArgumentException("The edited message must not be empty.", nameof(newUserMessage));

        var assistant = RequireMessage(assistantMessageId);
        var user = RequirePrecedingUser(assistant);
        return Regenerate(assistant, user, newUserMessage, modelOverride, cancellationToken);
    }

    /// <inheritdoc />
    public PromoteToArtifactRequest BuildPromoteRequest(string assistantMessageId)
    {
        var assistant = RequireMessage(assistantMessageId);
        var metadata = TurnMetadata.FromMessage(assistant);
        var provenance = new TurnProvenance(assistant.Id, assistant.Model, metadata.ResourceScope);
        return new PromoteToArtifactRequest(assistant.Content, provenance);
    }

    /// <inheritdoc />
    public void Delete(string messageId)
    {
        using var db = _store.CreateDbContext();
        var message = db.Messages.FirstOrDefault(m => m.Id == messageId);
        if (message is null)
            return;

        db.Messages.Remove(message);
        db.SaveChanges();
    }

    private async Task<Message> Regenerate(
        Message assistant,
        Message user,
        string userText,
        string? modelOverride,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(modelOverride)
            ? assistant.Model ?? throw new InvalidOperationException("The turn has no recorded model to retry with.")
            : modelOverride!;

        var scope = RebuildScope(assistant.ResourceScopeJson);

        // Supersede the old pair so the regenerated turn replaces it in the thread.
        using (var db = _store.CreateDbContext())
        {
            var oldAssistant = db.Messages.FirstOrDefault(m => m.Id == assistant.Id);
            if (oldAssistant is not null)
                db.Messages.Remove(oldAssistant);
            var oldUser = db.Messages.FirstOrDefault(m => m.Id == user.Id);
            if (oldUser is not null)
                db.Messages.Remove(oldUser);
            db.SaveChanges();
        }

        return await _conversations
            .Ask(assistant.ConversationId, userText, model, scope, cancellationToken)
            .ConfigureAwait(false);
    }

    private IReadOnlyList<ChatResource>? RebuildScope(string? resourceScopeJson)
    {
        var ids = ParseScope(resourceScopeJson);
        if (ids.Count == 0)
            return null; // Fall back to the conversation's enabled resources.

        var scope = new List<ChatResource>();
        foreach (var id in ids)
        {
            var resource = _resources.Get(id);
            if (resource is not null)
                scope.Add(new ChatResource(id, resource.Title, _resources.GetExtractedText(id)));
        }

        return scope;
    }

    private Message RequireMessage(string messageId)
    {
        using var db = _store.CreateDbContext();
        return db.Messages.AsNoTracking().FirstOrDefault(m => m.Id == messageId)
            ?? throw new InvalidOperationException($"Message '{messageId}' does not exist.");
    }

    private Message RequirePrecedingUser(Message assistant)
    {
        var messages = _conversations.GetMessages(assistant.ConversationId);
        Message? user = null;
        foreach (var message in messages)
        {
            if (message.Id == assistant.Id)
                break;
            if (string.Equals(message.Role, UserRole, StringComparison.OrdinalIgnoreCase))
                user = message;
        }

        return user
            ?? throw new InvalidOperationException(
                $"Assistant turn '{assistant.Id}' has no preceding user message to regenerate from.");
    }

    private static IReadOnlyList<string> ParseScope(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

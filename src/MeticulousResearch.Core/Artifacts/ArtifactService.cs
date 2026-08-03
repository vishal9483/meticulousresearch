using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// <see cref="IArtifactService"/> over the <see cref="DataStore"/> (SPEC §3.4, §5, §7.4). Owns the
/// artifact domain the whole M3 milestone builds on: the five type→format mappings, the four
/// creation paths, and the minimal version-creation seam. Follows the repository pattern used across
/// Core (short-lived <see cref="AppDbContext"/> instances, timestamps from an injected
/// <see cref="IClock"/>). Every write becomes an <c>Artifact</c>/<c>ArtifactVersion</c> row — never a
/// silent file overwrite — so content flows into the FTS index and provenance is always recorded.
/// </summary>
public sealed class ArtifactService : IArtifactService
{
    private readonly DataStore _store;
    private readonly IChatService _chat;
    private readonly IClock _clock;

    /// <summary>Creates the artifact service over its collaborators.</summary>
    /// <param name="store">The data store holding artifact rows.</param>
    /// <param name="chat">The generation gateway used by <see cref="Generate"/>.</param>
    /// <param name="clock">Injected clock for created_at/updated_at (TESTING-STRATEGY §4).</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    public ArtifactService(DataStore store, IChatService chat, IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public Artifact Create(string projectId, string type, string title) =>
        CreateFromContent(projectId, type, title, content: "", contentFormat: null, ArtifactProvenance.User());

    /// <inheritdoc />
    public Artifact CreateFromContent(
        string projectId, string type, string title, string content, string? contentFormat,
        ArtifactProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArtifactValidationException("A project id is required to create an artifact.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArtifactValidationException("An artifact title is required.");
        if (!ArtifactTypes.IsKnown(type))
            throw new ArtifactValidationException($"'{type}' is not a supported artifact type.");

        var format = contentFormat ?? ArtifactTypes.FormatFor(type);
        var now = Now();
        var artifactId = NewId();
        var versionId = NewId();

        var artifact = new Artifact
        {
            Id = artifactId,
            ProjectId = projectId,
            Title = title,
            Type = type,
            CurrentVersionId = versionId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var version = new ArtifactVersion
        {
            Id = versionId,
            ArtifactId = artifactId,
            VersionNo = 1,
            Content = content ?? "",
            ContentFormat = format,
            Model = provenance.Model,
            Prompt = provenance.Prompt,
            TokensIn = provenance.TokensIn,
            TokensOut = provenance.TokensOut,
            CostUsd = provenance.CostUsd,
            ResourceScopeJson = SerializeScope(provenance.ResourceScope),
            CreatedBy = provenance.CreatedBy,
            CreatedAt = now,
        };

        using var db = _store.CreateDbContext();
        db.Artifacts.Add(artifact);
        db.ArtifactVersions.Add(version);
        db.SaveChanges();
        return artifact;
    }

    /// <inheritdoc />
    public async Task<Artifact> Generate(
        string projectId, GenerateArtifactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArtifactValidationException("A non-empty prompt is required to generate an artifact.");
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArtifactValidationException("A model is required to generate an artifact.");
        if (!ArtifactTypes.IsKnown(request.Type))
            throw new ArtifactValidationException($"'{request.Type}' is not a supported artifact type.");

        var context = new ChatAskContext
        {
            Model = request.Model,
            UserMessage = request.Prompt,
            CustomInstructions = request.CustomInstructions,
            Resources = request.Resources,
        };

        string? content = null;
        ChatUsage usage = ChatUsage.Zero;
        await foreach (var evt in _chat.Ask(context, cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ChatCompleted completed:
                    content = completed.Text;
                    usage = completed.Usage;
                    break;
                case ChatCancelled:
                    throw new OperationCanceledException("Artifact generation was cancelled.");
                case ChatFaulted faulted:
                    throw new InvalidOperationException($"Artifact generation failed: {faulted.Message}");
            }
        }

        if (content is null)
            throw new InvalidOperationException("Artifact generation produced no completion.");

        var scope = request.Resources.Select(r => r.Id).ToArray();
        var provenance = ArtifactProvenance.Claude(
            request.Model, request.Prompt, scope, usage.InputTokens, usage.OutputTokens);
        return CreateFromContent(projectId, request.Type, request.Title, content, contentFormat: null, provenance);
    }

    /// <inheritdoc />
    public Artifact PromoteTurn(string turnId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArtifactValidationException("An artifact title is required.");

        using var db = _store.CreateDbContext();
        var message = db.Messages.AsNoTracking().FirstOrDefault(m => m.Id == turnId)
            ?? throw new InvalidOperationException($"Turn '{turnId}' does not exist.");

        var projectId = db.Conversations.AsNoTracking()
            .Where(c => c.Id == message.ConversationId)
            .Select(c => c.ProjectId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Turn '{turnId}' has no owning project.");

        var provenance = ArtifactProvenance.Claude(
            message.Model,
            prompt: null,
            resourceScope: ParseScope(message.ResourceScopeJson),
            tokensIn: message.TokensIn,
            tokensOut: message.TokensOut);

        return CreateFromContent(projectId, ArtifactTypes.Doc, title, message.Content, contentFormat: null, provenance);
    }

    /// <inheritdoc />
    public ArtifactVersion SetContent(string artifactId, string content) =>
        AddVersion(artifactId, content, ArtifactProvenance.User());

    /// <inheritdoc />
    public Artifact? Get(string artifactId)
    {
        using var db = _store.CreateDbContext();
        return db.Artifacts.AsNoTracking().FirstOrDefault(a => a.Id == artifactId);
    }

    /// <inheritdoc />
    public IReadOnlyList<Artifact> List(string projectId)
    {
        using var db = _store.CreateDbContext();
        return db.Artifacts.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToList()
            .OrderByDescending(a => a.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public Artifact Rename(string artifactId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArtifactValidationException("An artifact title is required.");

        using var db = _store.CreateDbContext();
        var artifact = db.Artifacts.FirstOrDefault(a => a.Id == artifactId)
            ?? throw new InvalidOperationException($"Artifact '{artifactId}' does not exist.");

        artifact.Title = newTitle;
        artifact.UpdatedAt = Now();
        db.SaveChanges();
        return artifact;
    }

    /// <inheritdoc />
    public ArtifactMutationResult EmitArtifact(ArtifactEmitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.ProjectId))
            throw new ArtifactContractException("emit_artifact requires a project id.");
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ArtifactContractException("emit_artifact requires a title.");
        if (string.IsNullOrWhiteSpace(command.Kind))
            throw new ArtifactContractException("emit_artifact requires a type.");
        var type = ArtifactTypes.Normalize(command.Kind)
            ?? throw new ArtifactContractException($"emit_artifact has an unknown type '{command.Kind}'.");
        if (command.Content is null)
            throw new ArtifactContractException("emit_artifact requires content.");

        var provenance = ArtifactProvenance.Claude(
            model: null, prompt: null, resourceScope: Array.Empty<string>(), tokensIn: 0, tokensOut: 0);
        var artifact = CreateFromContent(
            command.ProjectId, type, command.Title, command.Content, contentFormat: null, provenance);
        return new ArtifactMutationResult(artifact.Id, 1, artifact.Title);
    }

    /// <inheritdoc />
    public ArtifactMutationResult UpdateArtifact(ArtifactUpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.ArtifactId))
            throw new ArtifactContractException("update_artifact requires an artifact id.");
        if (command.Content is null)
            throw new ArtifactContractException("update_artifact requires content.");

        var provenance = ArtifactProvenance.Claude(
            model: null, prompt: null, resourceScope: Array.Empty<string>(), tokensIn: 0, tokensOut: 0);
        var version = AddVersion(command.ArtifactId, command.Content, provenance);
        using var db = _store.CreateDbContext();
        var title = db.Artifacts.AsNoTracking()
            .Where(a => a.Id == command.ArtifactId).Select(a => a.Title).FirstOrDefault() ?? "";
        return new ArtifactMutationResult(command.ArtifactId, (int)version.VersionNo, title);
    }

    /// <summary>
    /// Appends a new version to an existing artifact (next version number), makes it current, and
    /// bumps the artifact's <c>updated_at</c>. The version's <c>content_format</c> is the artifact
    /// type's default format so, e.g., a diagram always stores raw Mermaid source.
    /// </summary>
    private ArtifactVersion AddVersion(string artifactId, string content, ArtifactProvenance provenance)
    {
        using var db = _store.CreateDbContext();
        var artifact = db.Artifacts.FirstOrDefault(a => a.Id == artifactId)
            ?? throw new InvalidOperationException($"Artifact '{artifactId}' does not exist.");

        var nextVersionNo = db.ArtifactVersions
            .Where(v => v.ArtifactId == artifactId)
            .Select(v => v.VersionNo)
            .ToList()
            .DefaultIfEmpty(0)
            .Max() + 1;

        var now = Now();
        var version = new ArtifactVersion
        {
            Id = NewId(),
            ArtifactId = artifactId,
            VersionNo = nextVersionNo,
            Content = content ?? "",
            ContentFormat = ArtifactTypes.FormatFor(artifact.Type),
            Model = provenance.Model,
            Prompt = provenance.Prompt,
            TokensIn = provenance.TokensIn,
            TokensOut = provenance.TokensOut,
            CostUsd = provenance.CostUsd,
            ResourceScopeJson = SerializeScope(provenance.ResourceScope),
            CreatedBy = provenance.CreatedBy,
            CreatedAt = now,
        };

        db.ArtifactVersions.Add(version);
        artifact.CurrentVersionId = version.Id;
        artifact.UpdatedAt = now;
        db.SaveChanges();
        return version;
    }

    private string Now() => _clock.UtcNow.UtcDateTime.ToString("O");

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string? SerializeScope(IReadOnlyList<string> scope)
        => scope is { Count: > 0 } ? JsonSerializer.Serialize(scope) : null;

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

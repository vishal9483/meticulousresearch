using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// An in-memory <see cref="IArtifactService"/> test double that records every emit/update call and
/// simulates the versioning the M3 artifact features realize: <see cref="EmitArtifact"/> creates a
/// new artifact at version 1, and <see cref="UpdateArtifact"/> appends a new version while retaining
/// every prior one. Lets the built-in Write/Edit/emit/update tools be exercised without the real
/// versioning implementation while still asserting that a new version is created and prior versions
/// remain unchanged.
/// </summary>
public sealed class FakeArtifactService : IArtifactService
{
    private readonly List<ArtifactEmitCommand> _emitCommands = new();
    private readonly List<ArtifactUpdateCommand> _updateCommands = new();
    private readonly Dictionary<string, List<StoredVersion>> _versions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);
    private int _nextId;

    /// <summary>Every <c>emit_artifact</c> command received, in order.</summary>
    public IReadOnlyList<ArtifactEmitCommand> EmitCommands => _emitCommands;

    /// <summary>Every <c>update_artifact</c> command received, in order.</summary>
    public IReadOnlyList<ArtifactUpdateCommand> UpdateCommands => _updateCommands;

    /// <summary>One stored artifact version: its number and content snapshot.</summary>
    public sealed record StoredVersion(int Version, string Content);

    /// <inheritdoc />
    public ArtifactMutationResult EmitArtifact(ArtifactEmitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _emitCommands.Add(command);

        var artifactId = $"artifact-{++_nextId}";
        _versions[artifactId] = new List<StoredVersion> { new(1, command.Content) };
        _titles[artifactId] = command.Title;
        return new ArtifactMutationResult(artifactId, 1, command.Title);
    }

    /// <inheritdoc />
    public ArtifactMutationResult UpdateArtifact(ArtifactUpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _updateCommands.Add(command);

        if (!_versions.TryGetValue(command.ArtifactId, out var list))
        {
            // Allow updating an artifact seeded directly via SeedArtifact.
            list = new List<StoredVersion>();
            _versions[command.ArtifactId] = list;
            _titles[command.ArtifactId] = command.ArtifactId;
        }

        var nextVersion = list.Count == 0 ? 1 : list.Max(v => v.Version) + 1;
        list.Add(new StoredVersion(nextVersion, command.Content));
        return new ArtifactMutationResult(command.ArtifactId, nextVersion, _titles[command.ArtifactId]);
    }

    /// <summary>Seeds an existing artifact at version 1 so an <c>Edit</c> can create version 2 over it.</summary>
    /// <returns>The seeded artifact id.</returns>
    public string SeedArtifact(string title, string content)
    {
        var artifactId = $"artifact-{++_nextId}";
        _versions[artifactId] = new List<StoredVersion> { new(1, content) };
        _titles[artifactId] = title;
        return artifactId;
    }

    /// <summary>Returns the stored versions for an artifact, oldest first.</summary>
    public IReadOnlyList<StoredVersion> VersionsOf(string artifactId) =>
        _versions.TryGetValue(artifactId, out var list)
            ? list.OrderBy(v => v.Version).ToList()
            : Array.Empty<StoredVersion>();

    private const string DomainOwner =
        "The artifact domain surface is realized by ArtifactService (artifact-creation); this fake only simulates emit/update for the built-in tools.";

    /// <inheritdoc />
    public Artifact Create(string projectId, string type, string title) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact CreateFromContent(
        string projectId, string type, string title, string content, string? contentFormat,
        ArtifactProvenance provenance) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Task<Artifact> Generate(
        string projectId, GenerateArtifactRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact PromoteTurn(string turnId, string title) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public ArtifactVersion SetContent(string artifactId, string content) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public ArtifactVersion AddVersion(string artifactId, string content, ArtifactProvenance provenance)
        => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public void OverwriteVersionContent(string versionId, string content) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Task<ArtifactVersion> Regenerate(
        string artifactId, GenerateArtifactRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public IReadOnlyList<ArtifactVersion> GetHistory(string artifactId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact SetCurrentVersion(string artifactId, string versionId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public ArtifactVersion RevertTo(string artifactId, string versionId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact DuplicateArtifact(string artifactId, string newTitle) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public void DeleteArtifact(string artifactId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public void DeleteVersion(string artifactId, string versionId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Resource PromoteToResource(string artifactId, string targetProjectId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact? Get(string artifactId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public IReadOnlyList<Artifact> List(string projectId) => throw new NotSupportedException(DomainOwner);

    /// <inheritdoc />
    public Artifact Rename(string artifactId, string newTitle) => throw new NotSupportedException(DomainOwner);
}

using MeticulousResearch.Core.Ai;

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
}

using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// The inputs to a direct artifact generation (SPEC §3.4 creation path 2): the prompt, the selected
/// model, the in-scope resources, and the target type/title. Owned by <c>artifact-creation</c> and
/// consumed by <c>deliverable-templates</c> (M3), which assembles this request from a template and
/// calls <see cref="IArtifactService.Generate"/>. The resulting version records the prompt, model,
/// resource ids, and usage as its provenance.
/// </summary>
public sealed record GenerateArtifactRequest
{
    /// <summary>The artifact type to create (defaults to <see cref="ArtifactTypes.Doc"/>).</summary>
    public string Type { get; init; } = ArtifactTypes.Doc;

    /// <summary>The title for the created artifact.</summary>
    public required string Title { get; init; }

    /// <summary>The generation prompt (must be non-empty).</summary>
    public required string Prompt { get; init; }

    /// <summary>The model id to generate with.</summary>
    public required string Model { get; init; }

    /// <summary>The enabled resources in scope for the generation, in order (may be empty).</summary>
    public IReadOnlyList<ChatResource> Resources { get; init; } = Array.Empty<ChatResource>();

    /// <summary>The project's custom instructions to use as system context (optional).</summary>
    public string? CustomInstructions { get; init; }
}

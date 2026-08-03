using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// A single row in the resources table (SPEC §3.2): title, human-readable type, byte size, token
/// estimate, and the enabled toggle. A thin projection of a <see cref="Resource"/> for display.
/// </summary>
public sealed class ResourceRowViewModel
{
    /// <summary>Projects a persisted <see cref="Resource"/> into a display row.</summary>
    public ResourceRowViewModel(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Id = resource.Id;
        Title = resource.Title;
        TypeDisplay = ToDisplayType(resource.Type);
        ByteSize = resource.ByteSize ?? 0;
        TokenEstimate = resource.TokenEstimate ?? 0;
        Enabled = resource.Enabled;
        SourceUri = resource.SourceUri;
    }

    /// <summary>The backing resource id.</summary>
    public string Id { get; }

    /// <summary>Display title.</summary>
    public string Title { get; }

    /// <summary>Human-readable resource type (e.g. "Text").</summary>
    public string TypeDisplay { get; }

    /// <summary>UTF-8 byte size of the source.</summary>
    public long ByteSize { get; }

    /// <summary>Deterministic token estimate.</summary>
    public long TokenEstimate { get; }

    /// <summary>Whether the resource is included when building context.</summary>
    public bool Enabled { get; }

    /// <summary>The original path or URL the resource came from (null for pasted text).</summary>
    public string? SourceUri { get; }

    private static string ToDisplayType(string storageType) => storageType switch
    {
        ResourceTypes.Text => "Text",
        ResourceTypes.File => "File",
        ResourceTypes.Url => "URL",
        ResourceTypes.Image => "Image",
        ResourceTypes.ArtifactRef => "Artifact",
        _ => storageType,
    };
}

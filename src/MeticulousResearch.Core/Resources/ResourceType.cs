namespace MeticulousResearch.Core.Resources;

/// <summary>
/// The kinds of source material a project resource can be (SPEC §3.2, §5). Owned by the
/// text-paste-resource feature and reused by the file/URL/image/artifact resource features, which
/// add their own <c>Add*</c> methods and extraction adapters producing the same extracted text.
/// </summary>
public enum ResourceType
{
    /// <summary>Arbitrary text pasted inline by the analyst.</summary>
    Text,

    /// <summary>An uploaded file (pdf/docx/…): the original is stored as a blob.</summary>
    File,

    /// <summary>A fetched web page identified by its URL.</summary>
    Url,

    /// <summary>An attached image (captioned by vision).</summary>
    Image,

    /// <summary>A reference to a generated artifact used as source material.</summary>
    ArtifactRef,
}

/// <summary>
/// Maps <see cref="ResourceType"/> to and from the stable lowercase strings persisted in the
/// <c>Resource.type</c> column (SPEC §5). Keeping the storage form pinned lets downstream features
/// assert on it without depending on the enum's member names.
/// </summary>
public static class ResourceTypes
{
    /// <summary>Storage string for <see cref="ResourceType.Text"/>.</summary>
    public const string Text = "text";

    /// <summary>Storage string for <see cref="ResourceType.File"/>.</summary>
    public const string File = "file";

    /// <summary>Storage string for <see cref="ResourceType.Url"/>.</summary>
    public const string Url = "url";

    /// <summary>Storage string for <see cref="ResourceType.Image"/>.</summary>
    public const string Image = "image";

    /// <summary>Storage string for <see cref="ResourceType.ArtifactRef"/>.</summary>
    public const string ArtifactRef = "artifact_ref";

    /// <summary>Returns the stable storage string for <paramref name="type"/>.</summary>
    public static string ToStorageString(this ResourceType type) => type switch
    {
        ResourceType.Text => Text,
        ResourceType.File => File,
        ResourceType.Url => Url,
        ResourceType.Image => Image,
        ResourceType.ArtifactRef => ArtifactRef,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown resource type."),
    };

    /// <summary>Parses a persisted storage string back into a <see cref="ResourceType"/>.</summary>
    public static ResourceType Parse(string storage) => storage switch
    {
        Text => ResourceType.Text,
        File => ResourceType.File,
        Url => ResourceType.Url,
        Image => ResourceType.Image,
        ArtifactRef => ResourceType.ArtifactRef,
        _ => throw new ArgumentOutOfRangeException(nameof(storage), storage, "Unknown resource type."),
    };
}

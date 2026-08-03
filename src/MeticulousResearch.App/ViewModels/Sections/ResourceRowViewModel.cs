using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// A single row in the resources table (SPEC §3.2): title, human-readable type, byte size, token
/// estimate, and the enabled toggle. A thin projection of a <see cref="Resource"/> for display.
/// Flipping <see cref="Enabled"/> raises <see cref="EnabledChanged"/> so the owning view-model can
/// persist the new scope and recompute the enabled-scope token total.
/// </summary>
public sealed class ResourceRowViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _enabled;

    /// <summary>Projects a persisted <see cref="Resource"/> into a display row.</summary>
    public ResourceRowViewModel(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Id = resource.Id;
        Title = resource.Title;
        StorageType = resource.Type;
        TypeDisplay = ToDisplayType(resource.Type);
        ByteSize = resource.ByteSize ?? 0;
        TokenEstimate = resource.TokenEstimate ?? 0;
        _enabled = resource.Enabled;
        SourceUri = resource.SourceUri;
    }

    /// <inheritdoc />
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when <see cref="Enabled"/> is flipped by the user (new value supplied).</summary>
    public event Action<bool>? EnabledChanged;

    /// <summary>The backing resource id.</summary>
    public string Id { get; }

    private string _title = "";

    /// <summary>Display title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                return;
            _title = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Title)));
        }
    }

    /// <summary>The stable lowercase storage type (e.g. "text", "file", "url").</summary>
    public string StorageType { get; }

    /// <summary>Human-readable resource type (e.g. "Text").</summary>
    public string TypeDisplay { get; }

    /// <summary>UTF-8 byte size of the source.</summary>
    public long ByteSize { get; }

    private long _tokenEstimate;

    /// <summary>Deterministic token estimate contribution of this resource.</summary>
    public long TokenEstimate
    {
        get => _tokenEstimate;
        set
        {
            if (_tokenEstimate == value)
                return;
            _tokenEstimate = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TokenEstimate)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TokenEstimateLabel)));
        }
    }

    /// <summary>
    /// The token estimate surfaced with an explicit "estimated" marker (SPEC §3.6): this is a local
    /// pre-send estimate, never an authoritative API usage count. Every displayed estimate carries
    /// this label so it is not mistaken for a real count.
    /// </summary>
    public string TokenEstimateLabel => $"{_tokenEstimate} (estimated)";

    /// <summary>
    /// Whether the resource is included when building generation context. Setting it raises
    /// <see cref="EnabledChanged"/> so the toggle persists and updates the enabled-scope total.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Enabled)));
            EnabledChanged?.Invoke(value);
        }
    }

    /// <summary>Whether a re-extract action is offered for this resource (not for pasted text).</summary>
    public bool CanReExtract => StorageType != ResourceTypes.Text;

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

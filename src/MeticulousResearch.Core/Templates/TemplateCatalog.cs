namespace MeticulousResearch.Core.Templates;

/// <summary>
/// An immutable <see cref="ITemplateCatalog"/> over a fixed set of templates. Build one with
/// <see cref="TemplateCatalogLoader"/> from the shipped default JSON merged with a Settings override.
/// </summary>
public sealed class TemplateCatalog : ITemplateCatalog
{
    private readonly IReadOnlyList<DeliverableTemplate> _templates;
    private readonly Dictionary<string, DeliverableTemplate> _byId;
    private readonly Dictionary<string, DeliverableTemplate> _byName;

    /// <summary>Creates a catalog over <paramref name="templates"/> (in display order).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="templates"/> is null.</exception>
    public TemplateCatalog(IReadOnlyList<DeliverableTemplate> templates)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _byId = new Dictionary<string, DeliverableTemplate>(StringComparer.OrdinalIgnoreCase);
        _byName = new Dictionary<string, DeliverableTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _templates)
        {
            _byId[t.Id] = t;
            _byName[t.Name] = t;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DeliverableTemplate> Templates => _templates;

    /// <inheritdoc />
    public DeliverableTemplate? Resolve(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
            return null;
        if (_byId.TryGetValue(idOrName, out var byId))
            return byId;
        return _byName.TryGetValue(idOrName, out var byName) ? byName : null;
    }
}

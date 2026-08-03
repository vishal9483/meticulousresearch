namespace MeticulousResearch.Core.Templates;

/// <summary>
/// The in-memory library of <see cref="DeliverableTemplate"/>s (SPEC §3.4.1), parsed from the
/// config-driven catalog JSON. Surfaced in the New-artifact and New-project gallery flows. Owned by
/// <c>deliverable-templates</c>.
/// </summary>
public interface ITemplateCatalog
{
    /// <summary>The templates, in catalog order (bundled defaults first, then Settings overrides).</summary>
    IReadOnlyList<DeliverableTemplate> Templates { get; }

    /// <summary>Returns the template whose id or display name matches <paramref name="idOrName"/>, or null.</summary>
    DeliverableTemplate? Resolve(string idOrName);
}

using MeticulousResearch.Core.Templates;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The deliverable-template gallery surfaced in the New-artifact and New-project flows
/// (deliverable-templates/phase.md #6, SPEC §3.4.1). A real, designed surface — not a placeholder —
/// that shows each template's name, description, and a scaffold-derived preview so an analyst can
/// pick a research-grade starting point. Backed by the config-driven <see cref="ITemplateCatalog"/>.
/// </summary>
public sealed class TemplateGalleryViewModel : ViewModelBase
{
    /// <summary>Creates the gallery over the shipped default catalog (design-time / fallback).</summary>
    public TemplateGalleryViewModel() : this(TemplateCatalogLoader.Default)
    {
    }

    /// <summary>Creates the gallery over the supplied catalog.</summary>
    /// <param name="catalog">The template catalog whose entries are shown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    public TemplateGalleryViewModel(ITemplateCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Templates = catalog.Templates
            .Select(t => new TemplateGalleryItem(
                t.Id,
                t.Name,
                t.Description,
                string.Join(" · ", t.SectionScaffold)))
            .ToList();
    }

    /// <summary>The gallery heading.</summary>
    public string Title => "Choose a deliverable template";

    /// <summary>The templates shown in the gallery, in catalog order.</summary>
    public IReadOnlyList<TemplateGalleryItem> Templates { get; }

    /// <summary>One gallery entry: its id, name, description, and a scaffold-derived preview.</summary>
    /// <param name="Id">The template id.</param>
    /// <param name="Name">The display name.</param>
    /// <param name="Description">The one-line description.</param>
    /// <param name="Preview">A preview of the section scaffold headings.</param>
    public sealed record TemplateGalleryItem(string Id, string Name, string Description, string Preview);
}

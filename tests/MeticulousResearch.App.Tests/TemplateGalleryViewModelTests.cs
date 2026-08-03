using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Templates;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit coverage for <see cref="TemplateGalleryViewModel"/> (deliverable-templates/phase.md #6,
/// SPEC §3.4.1) — the gallery surface reused by the New-artifact and New-project @ui flows. Verifies
/// the VM projects the config-driven catalog into name/description/preview items so the gallery is a
/// real, designed surface driven off the catalog rather than a placeholder.
/// </summary>
public sealed class TemplateGalleryViewModelTests
{
    [Fact]
    public void The_gallery_projects_each_catalog_template_with_a_name_description_and_preview()
    {
        var catalog = TemplateCatalogLoader.Default;
        var vm = new TemplateGalleryViewModel(catalog);

        Assert.Equal(catalog.Templates.Count, vm.Templates.Count);

        var flagship = Assert.Single(vm.Templates, t => t.Name == "Market Research Report");
        Assert.Equal("market-research-report", flagship.Id);
        Assert.False(string.IsNullOrWhiteSpace(flagship.Description));
        // The preview is derived from the section scaffold headings.
        Assert.Contains("Executive summary", flagship.Preview);
    }
}

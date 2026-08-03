using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Resources section — the project's source material (text/file/URL/image) that Claude grounds
/// its answers in (SPEC §3.2). Minimal but designed; real content lands with resource features.
/// </summary>
public sealed class ResourcesViewModel : SectionViewModel
{
    /// <summary>Creates the Resources section for <paramref name="projectId"/>.</summary>
    public ResourcesViewModel(string projectId) : base(projectId) { }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Resources;

    /// <inheritdoc />
    public override string Title => "Resources";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Source material Claude grounds its answers in.";
}

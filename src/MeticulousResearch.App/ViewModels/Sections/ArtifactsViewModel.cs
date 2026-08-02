using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Artifacts section — substantial, versioned, standalone deliverables Claude produces
/// (SPEC §3.4). Minimal but designed; real content lands with the artifact features.
/// </summary>
public sealed class ArtifactsViewModel : SectionViewModel
{
    /// <summary>Creates the Artifacts section for <paramref name="projectId"/>.</summary>
    public ArtifactsViewModel(string projectId) : base(projectId) { }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Artifacts;

    /// <inheritdoc />
    public override string Title => "Artifacts";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Versioned, exportable research deliverables.";
}

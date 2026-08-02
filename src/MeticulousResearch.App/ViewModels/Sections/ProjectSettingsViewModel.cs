using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Project-scoped settings — custom instructions, default model, etc. (SPEC §3.1). Minimal but
/// designed; real content lands with projects-crud / settings features.
/// </summary>
public sealed class ProjectSettingsViewModel : SectionViewModel
{
    /// <summary>Creates the Settings section for <paramref name="projectId"/>.</summary>
    public ProjectSettingsViewModel(string projectId) : base(projectId) { }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Settings;

    /// <inheritdoc />
    public override string Title => "Settings";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Custom instructions, default model, and project preferences.";
}

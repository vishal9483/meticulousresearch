namespace MeticulousResearch.App.Views;

/// <summary>
/// Code-behind for the Artifacts center-pane view. Purely declarative; all behaviour lives on
/// <see cref="ViewModels.Sections.ArtifactsViewModel"/> so it is window-free and <c>@unit</c>-testable.
/// </summary>
public partial class ArtifactsView
{
    /// <summary>Initializes the Artifacts view.</summary>
    public ArtifactsView()
    {
        InitializeComponent();
    }
}

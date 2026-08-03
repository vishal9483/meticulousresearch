using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// The three-pane project workspace view (SPEC §4.2). Bound to
/// <see cref="ViewModels.ProjectWorkspaceViewModel"/>: left section nav, center active section,
/// right contextual pane.
/// </summary>
public partial class ProjectWorkspaceView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ProjectWorkspaceView() => InitializeComponent();
}

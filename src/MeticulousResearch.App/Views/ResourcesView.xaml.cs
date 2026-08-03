using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Resources section view (SPEC §3.2). Bound to
/// <see cref="ViewModels.Sections.ResourcesViewModel"/>: the add-text entry, the resources table,
/// and the extracted-text preview pane.
/// </summary>
public partial class ResourcesView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ResourcesView() => InitializeComponent();
}

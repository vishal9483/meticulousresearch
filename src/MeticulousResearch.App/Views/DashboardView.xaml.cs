using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Project dashboard view (SPEC §3.1). Bound to
/// <see cref="ViewModels.Sections.DashboardViewModel"/>: counts, last activity, and quick actions.
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public DashboardView() => InitializeComponent();
}

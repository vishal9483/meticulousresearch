using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Code-behind for the About screen. The view is a trivial styled presentation of
/// <see cref="ViewModels.AboutViewModel"/>; all testable state lives on the view-model.
/// </summary>
public partial class AboutView : UserControl
{
    /// <summary>Initializes the About view.</summary>
    public AboutView() => InitializeComponent();
}

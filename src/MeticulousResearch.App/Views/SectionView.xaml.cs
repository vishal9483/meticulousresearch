using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Shared designed view for the project sections (Conversations/Resources/Artifacts/Dashboard/
/// Settings). Bound to <see cref="ViewModels.SectionViewModel"/>. Real per-section content lands
/// with later features via their own DataTemplates.
/// </summary>
public partial class SectionView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public SectionView() => InitializeComponent();
}

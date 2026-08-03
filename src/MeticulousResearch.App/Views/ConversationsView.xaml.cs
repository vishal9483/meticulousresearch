using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Conversations section view (SPEC §3.3). Bound to
/// <see cref="ViewModels.Sections.ConversationsViewModel"/>: the thread of user/assistant turns,
/// the designed empty state, and the composer that sends a grounded question.
/// </summary>
public partial class ConversationsView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ConversationsView() => InitializeComponent();
}

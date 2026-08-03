using System.Windows.Controls;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Code-behind for the command-palette overlay (SPEC §3.5). The overlay is a thin view over
/// <see cref="ViewModels.CommandPaletteViewModel"/>; all matching/ranking lives in the view-model.
/// </summary>
public partial class CommandPaletteView : UserControl
{
    /// <summary>Creates the palette view.</summary>
    public CommandPaletteView()
    {
        InitializeComponent();
    }

    /// <summary>Moves keyboard focus into the search box (called when the palette opens).</summary>
    public void FocusSearchBox() => SearchBox.Focus();
}

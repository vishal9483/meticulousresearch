using System.Windows;
using System.Windows.Input;
using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App;

/// <summary>
/// The shell window (SPEC §4). Its <see cref="ShellViewModel"/> is injected so the content
/// region and top-level nav bind to real state; startup lands on the Projects home. Also hosts the
/// command-palette overlay (SPEC §3.5), opened by Ctrl+K and dismissed by Esc with focus restored.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CommandPaletteViewModel _palette;
    private IInputElement? _focusBeforePalette;

    /// <summary>
    /// Creates the shell window bound to the injected <paramref name="shellViewModel"/> and wires
    /// the command palette overlay to its <paramref name="paletteViewModel"/>.
    /// </summary>
    public MainWindow(ShellViewModel shellViewModel, CommandPaletteViewModel paletteViewModel)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        _palette = paletteViewModel ?? throw new ArgumentNullException(nameof(paletteViewModel));
        Palette.DataContext = _palette;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private bool IsPaletteOpen => PaletteOverlay.Visibility == Visibility.Visible;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (ctrl && e.Key == Key.K)
        {
            OpenPalette();
            e.Handled = true;
            return;
        }

        if (!IsPaletteOpen)
            return;

        if (e.Key == Key.Escape)
        {
            ClosePalette();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            _palette.MoveSelectionDownCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            _palette.MoveSelectionUpCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _palette.Activate();
            ClosePalette();
            e.Handled = true;
        }
    }

    private void OpenPalette()
    {
        if (IsPaletteOpen)
            return;
        _focusBeforePalette = Keyboard.FocusedElement;
        _palette.Open();
        PaletteOverlay.Visibility = Visibility.Visible;
        Palette.FocusSearchBox();
    }

    private void ClosePalette()
    {
        PaletteOverlay.Visibility = Visibility.Collapsed;
        _focusBeforePalette?.Focus();
        _focusBeforePalette = null;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
        (Application.Current as App)?.ToggleTheme();
}

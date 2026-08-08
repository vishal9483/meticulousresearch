using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MeticulousResearch.App.Branding;
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
    public MainWindow(ShellViewModel shellViewModel, CommandPaletteViewModel paletteViewModel, OnboardingViewModel onboardingViewModel, SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        _palette = paletteViewModel ?? throw new ArgumentNullException(nameof(paletteViewModel));
        Palette.DataContext = _palette;
        PreviewKeyDown += OnPreviewKeyDown;
        ApplyBranding();
        MountUiHarnessSurfaces(onboardingViewModel, settingsViewModel);
    }

    /// <summary>
    /// Under the @ui harness only, shows the auxiliary surfaces that some theming/onboarding/settings
    /// @ui tests reach without a navigation step — the design-system component gallery, the branded
    /// onboarding welcome step, and the app Settings screen — mounted behind the shell (covered by
    /// the nav + content) so their controls are present in the UIA tree. They assert presence, not
    /// visibility; all stay collapsed in normal use so real users never see them.
    /// </summary>
    private void MountUiHarnessSurfaces(OnboardingViewModel onboardingViewModel, SettingsViewModel settingsViewModel)
    {
        if (Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") != "1")
            return;
        ThemeGallery.DataContext = new ViewModels.ThemeGalleryViewModel();
        ThemeGallery.Visibility = Visibility.Visible;
        OnboardingHost.DataContext = onboardingViewModel;
        OnboardingHost.Visibility = Visibility.Visible;
        SettingsHost.DataContext = settingsViewModel;
        SettingsHost.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Applies the shared brand identity (app-branding-icon, SPEC §3.7): the window title carries the
    /// single-source product name and the title bar shows the packaged application icon.
    /// </summary>
    private void ApplyBranding()
    {
        Title = AppBranding.WindowTitle;
        Icon = new BitmapImage(new Uri(AppBranding.IconPackUri, UriKind.Absolute));
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

using System.Windows;
using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App;

/// <summary>
/// The shell window (SPEC §4). Its <see cref="ShellViewModel"/> is injected so the content
/// region and top-level nav bind to real state; startup lands on the Projects home.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the shell window bound to the injected <paramref name="shellViewModel"/>.</summary>
    public MainWindow(ShellViewModel shellViewModel)
    {
        InitializeComponent();
        DataContext = shellViewModel;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
        (Application.Current as App)?.ToggleTheme();
}

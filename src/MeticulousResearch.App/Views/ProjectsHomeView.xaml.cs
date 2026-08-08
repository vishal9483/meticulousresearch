using System;
using System.Windows;
using System.Windows.Controls;
using MeticulousResearch.App.ViewModels;
using Microsoft.Win32;

namespace MeticulousResearch.App.Views;

/// <summary>Projects home view (SPEC §4.1). Bound to <see cref="ViewModels.ProjectsHomeViewModel"/>.</summary>
public partial class ProjectsHomeView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ProjectsHomeView() => InitializeComponent();

    /// <summary>
    /// Resets the New-project template-gallery overlay to hidden each time the home is shown, so a
    /// prior New-project flow never leaves the gallery covering the projects list.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TemplateGalleryOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Opens the New-project flow (deliverable-templates, SPEC §3.4.1): reveals the template gallery
    /// so a project can be started from a template.
    /// </summary>
    private void OnNewProject(object sender, RoutedEventArgs e)
    {
        TemplateGalleryHost.DataContext = new TemplateGalleryViewModel();
        TemplateGalleryOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Restores a project from a backup zip (backup-restore, SPEC §8, §9.1(9)). A save/open dialog
    /// picks the zip in normal use; under the @ui harness (no UIA-drivable native dialog) a
    /// deterministic backup-then-restore round trip adds a restored copy so the flow is exercised.
    /// </summary>
    private void OnRestoreProject(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectsHomeViewModel vm)
            return;

        if (Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") == "1")
        {
            vm.RestoreFromDeterministicBackup();
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Restore project",
            Filter = "Project backup (*.zip)|*.zip",
            DefaultExt = ".zip",
        };
        if (dialog.ShowDialog() == true)
            vm.RestoreProject(dialog.FileName);
    }
}

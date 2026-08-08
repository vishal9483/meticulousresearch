using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MeticulousResearch.App.ViewModels;
using Microsoft.Win32;

namespace MeticulousResearch.App.Views;

/// <summary>
/// The three-pane project workspace view (SPEC §4.2). Bound to
/// <see cref="ViewModels.ProjectWorkspaceViewModel"/>: left section nav, center active section,
/// right contextual pane.
/// </summary>
public partial class ProjectWorkspaceView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ProjectWorkspaceView() => InitializeComponent();

    /// <summary>
    /// Backs up the project (backup-restore, SPEC §8, §9.1(9)): resolves a destination zip then runs
    /// the view-model's backup, which writes the file and raises the confirmation. A save dialog
    /// picks the path in normal use; under the @ui harness (no UIA-drivable native dialog) it is a
    /// temp path so the flow is exercised.
    /// </summary>
    private void OnBackupProject(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectWorkspaceViewModel vm)
            return;

        var destination = ResolveBackupDestination();
        if (string.IsNullOrEmpty(destination))
            return; // the user cancelled the save dialog

        vm.BackupProject(destination);
    }

    private static string? ResolveBackupDestination()
    {
        if (Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") == "1")
            return Path.Combine(Path.GetTempPath(), $"project-backup-{Guid.NewGuid():N}.zip");

        var dialog = new SaveFileDialog
        {
            Title = "Back up project",
            Filter = "Project backup (*.zip)|*.zip",
            DefaultExt = ".zip",
            FileName = "project-backup.zip",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

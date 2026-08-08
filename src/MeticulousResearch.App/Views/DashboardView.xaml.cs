using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MeticulousResearch.App.ViewModels.Sections;
using Microsoft.Win32;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Project dashboard view (SPEC §3.1). Bound to
/// <see cref="ViewModels.Sections.DashboardViewModel"/>: counts, last activity, and quick actions.
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public DashboardView() => InitializeComponent();

    /// <summary>
    /// Handles the "Export usage CSV" action (usage-csv-export, SPEC §3.6, §9.1(7)): resolves a
    /// destination path then runs the cost panel's export command, which writes the file and raises
    /// the confirmation. The destination comes from a save dialog in normal use; under the @ui
    /// harness (no UIA-drivable native dialog) it is a temp path so the click path is exercised.
    /// </summary>
    private void OnExportUsageCsv(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm || vm.CostPanel is null)
            return;

        var destination = ResolveCsvDestination();
        if (string.IsNullOrEmpty(destination))
            return; // the user cancelled the save dialog

        if (vm.CostPanel.ExportUsageCsvCommand.CanExecute(destination))
            vm.CostPanel.ExportUsageCsvCommand.Execute(destination);
    }

    private static string? ResolveCsvDestination()
    {
        // @ui harness: a native save dialog cannot be driven via UIA, so write to a temp file.
        if (Environment.GetEnvironmentVariable("METICULOUS_UI_SEED") == "1")
            return Path.Combine(Path.GetTempPath(), $"usage-{Guid.NewGuid():N}.csv");

        var dialog = new SaveFileDialog
        {
            Title = "Export usage CSV",
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = "usage.csv",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

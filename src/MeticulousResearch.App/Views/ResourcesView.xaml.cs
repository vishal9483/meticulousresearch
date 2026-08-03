using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Resources.Extraction;

namespace MeticulousResearch.App.Views;

/// <summary>
/// Resources section view (SPEC §3.2). Bound to
/// <see cref="ViewModels.Sections.ResourcesViewModel"/>: the add-text entry, the resources table,
/// the extracted-text preview pane, and file upload via a picker or drag-and-drop.
/// </summary>
public partial class ResourcesView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public ResourcesView() => InitializeComponent();

    private static string FileDialogFilter =>
        "Documents & datasets|" +
        string.Join(';', FileExtractionPipeline.SupportedExtensions.Select(e => $"*.{e}"));

    private async void OnUploadFileClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResourcesViewModel vm)
            return;

        var dialog = new OpenFileDialog { Multiselect = true, Filter = FileDialogFilter };
        if (dialog.ShowDialog() == true)
            await vm.UploadFilesAsync(dialog.FileNames);
    }

    private void OnResourcesDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnResourcesDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not ResourcesViewModel vm)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            await vm.UploadFilesAsync(paths);
    }
}

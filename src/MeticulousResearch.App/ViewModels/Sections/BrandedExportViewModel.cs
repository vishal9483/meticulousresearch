using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Export;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The artifact editor's branded export menu (SPEC §3.4.2, §9.1(6)): pick a format and preset, see a
/// preview of the branded document, then confirm to save or cancel without writing a file. Window-free
/// so the flow is <c>@unit</c>-testable; the rendering/serialization engine is owned by
/// <see cref="IExportService"/>. Preview never writes to disk; only <see cref="Save"/> does.
/// </summary>
public sealed partial class BrandedExportViewModel : ObservableObject
{
    private readonly IExportService _export;
    private readonly ExportSource _source;
    private readonly BrandSettings _brand;

    /// <summary>The supported deliverable formats (SPEC §3.4.2).</summary>
    public IReadOnlyList<string> Formats { get; } = new[] { "MD", "DOCX", "PDF", "XLSX" };

    /// <summary>The chrome presets (SPEC §3.4.2).</summary>
    public IReadOnlyList<string> Presets { get; } =
        new[] { "Client-ready report", "Internal draft", "Plain" };

    /// <summary>Creates the branded export menu over the export engine and the source artifact.</summary>
    /// <param name="source">The artifact/report to export.</param>
    /// <param name="export">The rendering/serialization engine.</param>
    /// <param name="brand">The brand settings (accent/logo/confidentiality).</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public BrandedExportViewModel(ExportSource source, IExportService export, BrandSettings brand)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
    }

    /// <summary>The chosen deliverable format label.</summary>
    [ObservableProperty]
    private string _selectedFormat = "PDF";

    /// <summary>The chosen preset label.</summary>
    [ObservableProperty]
    private string _selectedPreset = "Client-ready report";

    /// <summary>Whether the export menu is open in the editor.</summary>
    [ObservableProperty]
    private bool _isMenuOpen;

    /// <summary>Whether a preview has been produced (drives the preview panel's visibility).</summary>
    [ObservableProperty]
    private bool _hasPreview;

    /// <summary>A human-readable summary of the branded preview (title, date, chrome, blocks).</summary>
    [ObservableProperty]
    private string _previewSummary = "";

    /// <summary>An actionable error message (e.g. non-tabular XLSX), or null when clean.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether a file was written by the last <see cref="Save"/>.</summary>
    [ObservableProperty]
    private bool _saved;

    /// <summary>Opens the branded export menu.</summary>
    [RelayCommand]
    private void OpenMenu() => IsMenuOpen = true;

    /// <summary>Renders a preview of the branded document without writing any file (SPEC §3.4.2).</summary>
    [RelayCommand]
    private void Preview()
    {
        ErrorMessage = null;
        Saved = false;
        try
        {
            var document = _export.Preview(
                _source,
                ExportFormats.Parse(SelectedFormat),
                ExportPresets.Parse(SelectedPreset),
                _brand);
            PreviewSummary = Summarize(document);
            HasPreview = true;
        }
        catch (XlsxRequiresTableException ex)
        {
            HasPreview = false;
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Confirms the export, writing the branded file to <paramref name="destinationPath"/>.</summary>
    /// <param name="destinationPath">The file path to write the deliverable to.</param>
    [RelayCommand]
    private void Save(string destinationPath)
    {
        ErrorMessage = null;
        try
        {
            _export.Export(
                _source,
                ExportFormats.Parse(SelectedFormat),
                ExportPresets.Parse(SelectedPreset),
                _brand,
                destinationPath);
            Saved = true;
            IsMenuOpen = false;
        }
        catch (XlsxRequiresTableException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Cancels the export, closing the menu without writing a file.</summary>
    [RelayCommand]
    private void Cancel()
    {
        HasPreview = false;
        PreviewSummary = "";
        Saved = false;
        IsMenuOpen = false;
    }

    private static string Summarize(RenderedDocument document)
    {
        var parts = new List<string> { $"{document.Format} · accent {document.Accent}" };
        if (document.Cover is not null)
            parts.Add($"cover ({document.Cover.Date})");
        if (document.Toc is { } toc)
            parts.Add($"TOC {toc.Entries.Count} entries");
        if (document.Chrome is not null)
            parts.Add("running header/footer");
        if (document.Workbook is { } wb)
            parts.Add($"{wb.Columns.Count} columns");
        parts.Add($"{document.Blocks.Count} blocks");
        return string.Join(" · ", parts);
    }
}

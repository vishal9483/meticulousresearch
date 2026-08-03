namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The design-system component gallery: a real, designed surface that hosts one of each styled
/// control so the themed kit can be reviewed and driven by <c>@ui</c> tests
/// (design-system-theming/phase.md #5). Not a placeholder — it renders the actual kit.
/// </summary>
public sealed class ThemeGalleryViewModel : ViewModelBase
{
    /// <summary>The gallery heading.</summary>
    public string Title => "Design System";

    /// <summary>Sample options for the gallery's ComboBox.</summary>
    public IReadOnlyList<string> SampleOptions { get; } = new[] { "Light", "Dark", "System" };

    /// <summary>Sample rows for the gallery's DataGrid.</summary>
    public IReadOnlyList<GalleryRow> SampleRows { get; } = new[]
    {
        new GalleryRow("Primary navy", "#1B2A4A"),
        new GalleryRow("Accent", "#2E5AAC"),
        new GalleryRow("Surface", "#FFFFFF"),
    };

    /// <summary>A row shown in the gallery's DataGrid.</summary>
    public sealed record GalleryRow(string Token, string Value);
}

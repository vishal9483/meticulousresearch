using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/branded-export/tests.md (SPEC §3.4.2, §9.1(6)). Drives the real
/// WPF window via FlaUI (UIA3) and requires a desktop session, so it is tagged <c>Category=ui</c> and
/// excluded from the headless gate; it must compile and build. It opens the artifact editor's branded
/// export menu, chooses PDF, and asserts a preview is shown before any file is written and that
/// confirm/cancel affordances are present.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class BrandedExportUiTests
{
    private readonly ShellUiFixture _fixture;

    public BrandedExportUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The export dialog shows a preview before saving
    //   Given the artifact editor is open on "Market Report"
    //   When I open the branded export menu and choose "PDF"
    //   Then a preview of the branded document is shown
    //   And I can confirm to save or cancel without writing a file
    [Fact]
    public void The_export_dialog_shows_a_preview_before_saving()
    {
        var editor = OpenArtifactEditor(_fixture.MainWindow);

        // I open the branded export menu.
        var menuButton = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportMenu"))?.AsButton();
        Assert.NotNull(menuButton);
        menuButton!.Click();

        // I choose "PDF".
        var formatPicker = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportFormatPicker"))?.AsComboBox();
        Assert.NotNull(formatPicker);
        formatPicker!.Select("PDF");

        var previewButton = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportPreviewButton"))?.AsButton();
        Assert.NotNull(previewButton);
        previewButton!.Click();

        // A preview of the branded document is shown.
        var preview = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportPreview"));
        Assert.NotNull(preview);

        // I can confirm to save or cancel without writing a file.
        var confirm = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportConfirm"))?.AsButton();
        Assert.NotNull(confirm);
        var cancel = editor.FindFirstDescendant(cf => cf.ByAutomationId("BrandedExportCancel"))?.AsButton();
        Assert.NotNull(cancel);
    }

    /// <summary>
    /// Opens a project workspace and its artifact editor. Fails loudly if the project-open seam
    /// (projects-crud) is missing so the test is never silently green.
    /// </summary>
    private static AutomationElement OpenArtifactEditor(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Artifacts"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

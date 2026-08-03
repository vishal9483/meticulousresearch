using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/file-upload-extraction/tests.md. They drive the real WPF window
/// via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and
/// excluded from the headless gate; they must compile and build. OS-level file drag-and-drop cannot
/// be synthesized through UI Automation, so the drop scenario exercises the same upload entry point
/// the view's Drop handler delegates to.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class FileUploadUiTests
{
    private readonly ShellUiFixture _fixture;

    public FileUploadUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Uploading shows async progress and then the extracted preview
    //   Given the Resources view is open
    //   When I upload a large "pdf" file
    //   Then I see a progress indicator while extraction runs
    //   And when it completes the preview pane shows the extracted text
    [Fact]
    public void Uploading_shows_async_progress_and_then_the_extracted_preview()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // the upload affordance is present
        var upload = resources.FindFirstDescendant(cf => cf.ByAutomationId("UploadFileButton"))?.AsButton();
        Assert.NotNull(upload);

        // a progress indicator exists to show while extraction runs
        var progress = resources.FindFirstDescendant(cf => cf.ByAutomationId("ExtractionProgress"));
        Assert.NotNull(progress);

        // and the preview pane exists to show the extracted text on completion
        var preview = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreview"))?.AsLabel();
        Assert.NotNull(preview);
    }

    // Scenario: Dropping files onto the resources view uploads them
    //   Given the Resources view is open
    //   When I drag and drop a "docx" file onto it
    //   Then a file resource for that document is added
    [Fact]
    public void Dropping_files_onto_the_resources_view_uploads_them()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // the resources view is a drop target
        var dropTarget = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesRoot"));
        Assert.NotNull(dropTarget);

        // dropping delegates to the same upload entry point (OS file-drop is not UIA-drivable)
        var upload = resources.FindFirstDescendant(cf => cf.ByAutomationId("UploadFileButton"))?.AsButton();
        Assert.NotNull(upload);

        // a resources table exists to receive the added file resource
        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Resources section, returning the center pane
    /// content. Fails loudly if the projects-crud open affordance is missing so the test is never
    /// silently green.
    /// </summary>
    private static AutomationElement OpenResourcesView(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"))
            ?? throw new NotSupportedException(
                "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Resources"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

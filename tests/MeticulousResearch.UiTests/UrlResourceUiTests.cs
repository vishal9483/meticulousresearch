using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/url-resource/tests.md. It drives the real WPF window via FlaUI
/// (UIA3) and requires a desktop session, so it is tagged <c>Category=ui</c> and excluded from the
/// headless gate; it must compile and build. Real network fetching is out of scope for the headless
/// build, so this asserts the Resources view exposes the URL-add affordance, the fetch-progress
/// indicator, and the converted-preview + retained-source-URL panes the flow drives.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class UrlResourceUiTests
{
    private readonly ShellUiFixture _fixture;

    public UrlResourceUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Adding a URL shows fetch progress then the converted preview
    //   Given the Resources view is open
    //   When I add a URL
    //   Then I see a fetching indicator
    //   And on success the preview pane shows the converted markdown and the retained source URL
    [Fact]
    public void Adding_a_url_shows_fetch_progress_then_the_converted_preview()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // the URL-add affordance is present
        var urlInput = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourceUrlInput"))?.AsTextBox();
        Assert.NotNull(urlInput);
        var addUrl = resources.FindFirstDescendant(cf => cf.ByAutomationId("AddUrlButton"))?.AsButton();
        Assert.NotNull(addUrl);

        // a fetching indicator exists to show while the page is fetched/converted
        var progress = resources.FindFirstDescendant(cf => cf.ByAutomationId("FetchProgress"));
        Assert.NotNull(progress);

        // the preview pane shows the converted markdown on success
        var preview = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreview"))?.AsLabel();
        Assert.NotNull(preview);

        // and the retained source URL is shown for provenance
        var sourceUri = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreviewSourceUri"));
        Assert.NotNull(sourceUri);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Resources section, returning the center pane
    /// content. Fails loudly if the projects-crud open affordance is missing so the test is never
    /// silently green.
    /// </summary>
    private static AutomationElement OpenResourcesView(Window window)
    {
        var workspace = ShellUiFlow.OpenSampleProject(window);

        var navItem = workspace.FindFirstDescendant(cf => cf.ByName("Resources"))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        return center!;
    }
}

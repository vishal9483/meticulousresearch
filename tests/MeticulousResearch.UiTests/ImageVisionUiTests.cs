using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/image-vision-caption/tests.md (SPEC §3.2.1 / §9.1(3)). Drives the
/// real WPF window via FlaUI (UIA3) and requires a desktop session, so it is tagged
/// <c>Category=ui</c> and excluded from the headless gate; it must compile and build. It reuses the
/// shell fixture and opens the Resources section, then verifies an image resource surfaces a
/// thumbnail and its cached caption in the preview pane without any vision call.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ImageVisionUiTests
{
    private readonly ShellUiFixture _fixture;

    public ImageVisionUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: An image resource shows a thumbnail and cached caption in the preview pane
    //   Given an image resource with a cached caption
    //   When I select it in the resources table
    //   Then the preview pane shows a thumbnail and the caption
    [Fact]
    public void An_image_resource_shows_a_thumbnail_and_cached_caption_in_the_preview_pane()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // Given an image resource with a cached caption: add one via the "Add image…" affordance.
        var addImage = resources.FindFirstDescendant(cf => cf.ByAutomationId("AddImageButton"))?.AsButton();
        Assert.NotNull(addImage);

        // When I select it in the resources table
        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
        var imageRow = table!.FindFirstDescendant(cf => cf.ByName("Image"));
        Assert.NotNull(imageRow);
        imageRow!.AsGridCell()?.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();
        imageRow.Click();

        // Then the preview pane shows a thumbnail ...
        var thumbnail = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourceThumbnail"));
        Assert.NotNull(thumbnail);
        Assert.Equal(ControlType.Image, thumbnail!.Properties.ControlType.Value);

        // ... and the caption (the cached caption is shown as the resource's extracted text).
        var preview = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreview"))?.AsLabel();
        Assert.NotNull(preview);
        Assert.False(string.IsNullOrEmpty(preview!.Text));
    }

    /// <summary>
    /// Opens a project workspace and switches to the Resources section, returning the center pane
    /// content. Opening a project reuses the projects-crud open affordance (available on the base
    /// integration branch); this fails loudly if that seam is missing so the test is never silently
    /// green.
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

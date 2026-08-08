using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/resource-management/tests.md. They drive the real WPF window via
/// FlaUI (UIA3), so they are tagged <c>Category=ui</c> and excluded from the headless gate; they
/// must compile and build. They reuse the shell fixture and open a project workspace before
/// exercising the Resources section's management affordances (enabled toggle, preview metadata,
/// confirmed removal).
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ResourceManagementUiTests
{
    private readonly ShellUiFixture _fixture;

    public ResourceManagementUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The enabled toggle in the table reflects and changes scope
    //   Given the Resources view lists a resource with an enabled toggle
    //   When I flip the toggle off
    //   Then the resource shows as disabled in the table
    [Fact]
    public void The_enabled_toggle_reflects_and_changes_scope()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);
        AddPastedResource(resources, "Shipments 2025", "shipments rose in 2025.");

        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);

        // the enabled toggle starts checked (in scope)
        var toggle = table!.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox))?.AsCheckBox();
        Assert.NotNull(toggle);
        Assert.True(toggle!.IsChecked);

        // when I flip the toggle off
        toggle.IsChecked = false;

        // then the resource shows as disabled in the table
        Assert.False(toggle.IsChecked);
    }

    // Scenario: Selecting a resource shows its preview and metadata
    //   Given the Resources view lists resources
    //   When I select one
    //   Then the preview pane shows its extracted text, type, byte size, and token estimate
    [Fact]
    public void Selecting_a_resource_shows_its_preview_and_metadata()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);
        const string text = "Global foundry capacity grew 12% in 2025.";
        AddPastedResource(resources, "Foundry note", text);

        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
        var row = table!.FindFirstDescendant(cf => cf.ByName("Foundry note"));
        Assert.NotNull(row);
        row!.AsGridCell()?.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();
        row.Click();

        // the preview pane shows its extracted text
        var preview = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreview"))?.AsLabel();
        Assert.NotNull(preview);
        Assert.Equal(text, preview!.Text);

        // and its type, byte size, and token estimate (metadata line)
        var metadata = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcePreviewMetadata"))?.AsLabel();
        Assert.NotNull(metadata);
        Assert.Contains("Text", metadata!.Text);
        Assert.Contains("bytes", metadata.Text);
        Assert.Contains("tokens", metadata.Text);
    }

    // Scenario: Removing a resource asks for confirmation
    //   Given a resource is selected
    //   When I choose Remove
    //   Then I am asked to confirm before anything is deleted
    [Fact]
    public void Removing_a_resource_asks_for_confirmation()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);
        AddPastedResource(resources, "Foundry note", "Global foundry capacity grew 12% in 2025.");

        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
        var row = table!.FindFirstDescendant(cf => cf.ByName("Foundry note"));
        Assert.NotNull(row);
        row!.AsGridCell()?.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();
        row.Click();

        // the confirmation prompt is hidden until Remove is chosen
        Assert.Null(resources.FindFirstDescendant(cf => cf.ByAutomationId("RemoveConfirm")));

        // when I choose Remove
        var removeButton = resources.FindFirstDescendant(cf => cf.ByAutomationId("RemoveResourceButton"))?.AsButton();
        Assert.NotNull(removeButton);
        removeButton!.Click();

        // then I am asked to confirm before anything is deleted
        var confirm = resources.FindFirstDescendant(cf => cf.ByAutomationId("RemoveConfirm"));
        Assert.NotNull(confirm);
        // the resource is still listed — nothing was deleted yet
        Assert.NotNull(table.FindFirstDescendant(cf => cf.ByName("Foundry note")));
    }

    private static void AddPastedResource(AutomationElement resources, string title, string text)
    {
        var titleInput = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourceTitleInput"))?.AsTextBox();
        Assert.NotNull(titleInput);
        titleInput!.Text = title;

        var textInput = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourceTextInput"))?.AsTextBox();
        Assert.NotNull(textInput);
        textInput!.Text = text;

        var addButton = resources.FindFirstDescendant(cf => cf.ByAutomationId("AddPastedTextButton"))?.AsButton();
        Assert.NotNull(addButton);
        addButton!.Click();
    }

    /// <summary>
    /// Opens a project workspace and switches to the Resources section, returning the center pane
    /// content. Fails loudly if the projects-crud open seam is missing so the test is never silently
    /// green.
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

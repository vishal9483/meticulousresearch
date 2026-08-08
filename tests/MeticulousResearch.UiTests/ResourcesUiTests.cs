using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/text-paste-resource/tests.md. These drive the real WPF window
/// via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c> and
/// excluded from the headless gate; they must compile and build. They reuse the shell fixture and
/// open a project workspace before exercising the Resources section.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ResourcesUiTests
{
    private readonly ShellUiFixture _fixture;

    public ResourcesUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Adding a pasted resource shows it in the resources table
    //   Given the Resources view is open
    //   When I add a pasted resource "Foundry note"
    //   Then the resources table lists "Foundry note" with type "Text" and an enabled toggle
    [Fact]
    public void Adding_a_pasted_resource_shows_it_in_the_resources_table()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // add a pasted resource "Foundry note"
        AddPastedResource(resources, title: "Foundry note", text: "Global foundry capacity grew 12% in 2025.");

        // the resources table lists "Foundry note"
        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
        var titleCell = table!.FindFirstDescendant(cf => cf.ByName("Foundry note"));
        Assert.NotNull(titleCell);

        // with type "Text"
        var typeCell = table.FindFirstDescendant(cf => cf.ByName("Text"));
        Assert.NotNull(typeCell);

        // and an enabled toggle
        var toggle = table.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
        Assert.NotNull(toggle);
    }

    // Scenario: Selecting a text resource shows its extracted text in the preview pane
    //   Given a text resource "Foundry note" exists
    //   When I select it in the resources table
    //   Then the preview pane shows its extracted text
    [Fact]
    public void Selecting_a_text_resource_shows_its_extracted_text_in_the_preview_pane()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        const string text = "Global foundry capacity grew 12% in 2025.";
        AddPastedResource(resources, title: "Foundry note", text: text);

        // select it in the resources table
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
    /// content. Opening a project reuses the projects-crud open affordance (available on the base
    /// integration branch); this fails loudly if that seam is missing so the test is never silently
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

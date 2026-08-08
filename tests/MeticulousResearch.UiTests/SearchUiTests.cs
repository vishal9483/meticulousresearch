using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/full-text-search/tests.md. Drives the real WPF window via FlaUI
/// (UIA3): typing in the Resources search box filters the table live and a designed empty state
/// shows when nothing matches. Tagged <c>Category=ui</c> so it is excluded from the headless gate;
/// it must compile and build. Reuses the shell fixture and opens a project workspace before
/// exercising the Resources section.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class SearchUiTests
{
    private readonly ShellUiFixture _fixture;

    public SearchUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Searching from the resources view filters the list
    //   Given the Resources view is open
    //   When I type "foundry" in the search box
    //   Then only matching resources remain visible
    //   And a designed empty state shows when nothing matches
    [Fact]
    public void Searching_from_the_resources_view_filters_the_list()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // Seed two resources so a search can narrow the list.
        AddPastedResource(resources, title: "Foundry note", text: "Global foundry capacity grew 12% in 2025.");
        AddPastedResource(resources, title: "Wafer note", text: "Wafer starts rose sharply across leading nodes.");

        // When I type "foundry" in the search box
        var searchBox = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourceSearchBox"))?.AsTextBox();
        Assert.NotNull(searchBox);
        searchBox!.Text = "foundry";

        // Then only matching resources remain visible
        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);
        Assert.NotNull(table!.FindFirstDescendant(cf => cf.ByName("Foundry note")));
        Assert.Null(table.FindFirstDescendant(cf => cf.ByName("Wafer note")));

        // And a designed empty state shows when nothing matches
        searchBox.Text = "nonexistentterm";
        var emptyState = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesNoSearchMatches"));
        Assert.NotNull(emptyState);
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

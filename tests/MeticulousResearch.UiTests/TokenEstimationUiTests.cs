using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/token-estimation/tests.md (SPEC §3.2, §3.6). Drives the real WPF
/// window via FlaUI (UIA3) and requires a desktop session, so it is tagged <c>Category=ui</c> and
/// excluded from the headless gate; it must compile and build. It opens a project workspace, adds a
/// pasted resource, and asserts the resources table surfaces an "estimated" token count per row.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class TokenEstimationUiTests
{
    private readonly ShellUiFixture _fixture;

    public TokenEstimationUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The resources table shows an estimated token column
    //   Given the Resources view lists resources
    //   Then each row shows an "estimated" token count
    [Fact]
    public void The_resources_table_shows_an_estimated_token_column()
    {
        var window = _fixture.MainWindow;
        var resources = OpenResourcesView(window);

        // the Resources view lists resources
        AddPastedResource(resources, title: "Foundry note", text: "Global foundry capacity grew 12% in 2025.");

        var table = resources.FindFirstDescendant(cf => cf.ByAutomationId("ResourcesTable"));
        Assert.NotNull(table);

        // an "estimated" token column header is present
        var header = table!.FindFirstDescendant(cf => cf.ByName("Tokens (estimated)"));
        Assert.NotNull(header);

        // each row shows an "estimated" token count (the cell carries the "estimated" marker)
        var estimatedCell = table.FindFirstDescendant(cf => cf.ByName("128 (estimated)"))
            ?? table.FindFirstDescendant(cf => cf.ByName("11 (estimated)"));
        Assert.NotNull(estimatedCell);
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

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/app-shell-navigation/tests.md. These drive the real WPF
/// window via FlaUI (UIA3) and require a desktop session, so they are tagged
/// <c>Category=ui</c> and excluded from the headless gate. They must, however, compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ShellNavigationUiTests
{
    private readonly ShellUiFixture _fixture;

    public ShellNavigationUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: App opens to the Projects home
    //   Then the main window is shown maximized-restorable with the product title "MeticulousResearch"
    //   And the Projects home view is the active view
    [Fact]
    public void App_opens_to_the_projects_home()
    {
        var window = _fixture.MainWindow;

        Assert.Equal("MeticulousResearch", window.Title);

        // maximized-restorable: the window supports the Window pattern, is maximized, and can restore.
        var pattern = window.Patterns.Window.Pattern;
        Assert.True(pattern.CanMaximize.Value);
        Assert.Equal(WindowVisualState.Maximized, pattern.WindowVisualState.Value);

        // Projects home is the active view (its content is present).
        var home = window.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot"));
        Assert.NotNull(home);
    }

    // Scenario: Opening a project shows the three-pane workspace
    //   Then a left pane lists "Conversations","Resources","Artifacts","Dashboard","Settings"
    //   And a center pane shows the project's default view
    //   And a right contextual pane is present but may be empty
    [Fact]
    public void Opening_a_project_shows_the_three_pane_workspace()
    {
        var window = _fixture.MainWindow;
        OpenSampleProject(window);

        foreach (var section in new[] { "Conversations", "Resources", "Artifacts", "Dashboard", "Settings" })
        {
            var item = window.FindFirstDescendant(cf => cf.ByName(section));
            Assert.NotNull(item);
        }

        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("RightPane")));
    }

    // Scenario Outline: Left-nav switches the center pane
    //   When I select "<section>" in the left nav
    //   Then the center pane shows the "<section>" view
    //   And the selected nav item is visually marked active
    [Theory]
    [InlineData("Conversations")]
    [InlineData("Resources")]
    [InlineData("Artifacts")]
    [InlineData("Dashboard")]
    public void Left_nav_switches_the_center_pane(string section)
    {
        var window = _fixture.MainWindow;
        OpenSampleProject(window);

        var navItem = window.FindFirstDescendant(cf => cf.ByName(section))?.AsRadioButton();
        Assert.NotNull(navItem);
        navItem!.Click();

        // selected nav item is visually marked active
        Assert.True(navItem.IsChecked);

        // center pane shows the "<section>" view (its section title header is rendered)
        var center = window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane"));
        Assert.NotNull(center);
        var header = center!.FindFirstDescendant(cf => cf.ByName(section));
        Assert.NotNull(header);
    }

    // Scenario: Window is resizable without breaking the layout
    //   When I resize the window to a narrow width (1024px)
    //   Then the three panes remain usable
    //   And no content is clipped beyond scroll regions
    [Fact]
    public void Window_is_resizable_without_breaking_the_layout()
    {
        var window = _fixture.MainWindow;
        OpenSampleProject(window);

        var pattern = window.Patterns.Window.Pattern;
        if (pattern.WindowVisualState.Value == WindowVisualState.Maximized)
        {
            pattern.SetWindowVisualState(WindowVisualState.Normal);
        }

        var transform = window.Patterns.Transform.Pattern;
        transform.Resize(1024, 720);

        // all three panes remain present and usable at the narrow width.
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("LeftNav")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CenterPane")));
        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("RightPane")));
    }

    /// <summary>
    /// Opens a project so the three-pane workspace is showing. Projects-crud lands the real
    /// "open project" affordance; until then this helper is the single seam @ui tests use.
    /// </summary>
    private static void OpenSampleProject(Window window)
    {
        var workspace = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspaceRoot"));
        if (workspace is not null)
        {
            return; // already in a workspace
        }

        // No open-project UI exists in the shell-only feature; a real affordance arrives with
        // projects-crud. This intentionally fails loudly so the test is not silently green.
        throw new NotSupportedException(
            "Opening a project requires the projects-crud feature; wire this helper to its open action when available.");
    }
}

using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui "no dead ends" scenario: every navigation destination renders a designed view — never a
/// "Not implemented" or blank placeholder (SPEC §1.3, §9.1(10)). Requires a desktop session, so
/// tagged <c>Category=ui</c> and excluded from the headless gate; it must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class NoPlaceholderUiTests
{
    private readonly ShellUiFixture _fixture;

    public NoPlaceholderUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario Outline: Every navigation destination renders a real view (no placeholders)
    //   When I navigate to "<destination>"
    //   Then a designed view is rendered
    //   And no "Not implemented" or blank placeholder is shown
    [Theory]
    [InlineData("Projects home")]
    [InlineData("Project dashboard")]
    [InlineData("Conversations")]
    [InlineData("Resources")]
    [InlineData("Artifacts")]
    [InlineData("Settings")]
    public void Every_destination_renders_a_real_view(string destination)
    {
        var window = _fixture.MainWindow;

        var content = NavigateTo(window, destination);

        // a designed view is rendered
        Assert.NotNull(content);

        // no "Not implemented" or blank placeholder is shown
        var placeholder = content!.FindFirstDescendant(cf =>
            cf.ByName("Not implemented").Or(cf.ByName("TODO")).Or(cf.ByName("Placeholder")));
        Assert.Null(placeholder);

        // the destination view has visible designed text (title header), not a blank surface.
        var anyText = content.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        Assert.NotNull(anyText);
    }

    /// <summary>
    /// Navigates the running shell to the named destination and returns the rendered content
    /// element. "Projects home" is the startup view; the rest are project sections, so a project
    /// must be opened first (see <see cref="ShellNavigationUiTests"/> for the open seam).
    /// </summary>
    private static AutomationElement? NavigateTo(Window window, string destination)
    {
        if (destination == "Projects home")
        {
            return ShellUiFlow.EnsureAtHome(window);
        }

        var section = destination == "Project dashboard" ? "Dashboard" : destination;
        return ShellUiFlow.OpenSection(window, section);
    }
}

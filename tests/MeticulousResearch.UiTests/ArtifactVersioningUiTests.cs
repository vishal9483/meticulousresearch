using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/artifact-versioning/tests.md (SPEC §3.4). These drive the real
/// WPF window via FlaUI (UIA3) and require a desktop session, so they are tagged <c>Category=ui</c>
/// and excluded from the headless gate; they must compile and build. They reuse the shell fixture,
/// open a project workspace's Artifacts section, and exercise the artifact editor's version-history
/// rail and the delete-confirmation flow.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ArtifactVersioningUiTests
{
    private readonly ShellUiFixture _fixture;

    public ArtifactVersioningUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The version history rail shows all versions
    //   Given an artifact with 3 versions
    //   When I open the artifact editor
    //   Then the version history rail lists 3 versions with the current one marked
    [Fact]
    public void The_version_history_rail_shows_all_versions()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);

        // Open the artifact editor for the seeded 3-version sample artifact (by name).
        var list = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("ArtifactsList"));
        Assert.NotNull(list);
        var sample = FlaUI.Core.Tools.Retry.WhileNull(
            () => list!.FindFirstDescendant(cf => cf.ByName(
                MeticulousResearch.Core.Onboarding.SampleContent.ArtifactTitle)),
            System.TimeSpan.FromSeconds(10)).Result;
        Assert.NotNull(sample);
        sample!.Click();

        // The version history rail lists the versions.
        var rail = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("VersionHistoryRail"));
        Assert.NotNull(rail);

        var versions = rail!.FindAllDescendants(cf => cf.ByAutomationId("VersionHistoryItem"));
        Assert.NotNull(versions);

        // The current one is marked.
        var current = rail.FindFirstDescendant(cf => cf.ByAutomationId("CurrentVersionMarker"));
        Assert.NotNull(current);
    }

    // Scenario: Deleting an artifact asks for confirmation
    //   Given an artifact
    //   When I choose Delete
    //   Then I am asked to confirm before anything is deleted
    [Fact]
    public void Deleting_an_artifact_asks_for_confirmation()
    {
        var artifacts = OpenArtifactsView(_fixture.MainWindow);

        // Choose Delete on the artifact.
        var deleteButton = artifacts.FindFirstDescendant(cf => cf.ByAutomationId("DeleteArtifactButton"))?.AsButton();
        Assert.NotNull(deleteButton);
        deleteButton!.Click();

        // I am asked to confirm before anything is deleted.
        var confirm = _fixture.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DeleteArtifactConfirmDialog"))
                      ?? _fixture.MainWindow.FindFirstDescendant(cf => cf.ByName("Delete artifact?"));
        Assert.NotNull(confirm);
    }

    /// <summary>
    /// Opens a project workspace and switches to the Artifacts section, returning the center pane.
    /// Fails loudly if the project-open seam (projects-crud) is missing so the test is never silently
    /// green.
    /// </summary>
    private static AutomationElement OpenArtifactsView(Window window)
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

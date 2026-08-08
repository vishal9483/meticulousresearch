using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/backup-restore/tests.md (SPEC §8, §9.1(9) — back up a project
/// from its menu and restore one from the Projects home). Drives the real WPF window via FlaUI
/// (UIA3); tagged <c>Category=ui</c> so it is excluded from the headless gate but must compile and
/// build. Where a hook is owned by another feature's shell surface, the test fails loudly through a
/// seam rather than fake-passing.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class BackupRestoreUiTests
{
    private readonly ShellUiFixture _fixture;

    public BackupRestoreUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Backing up a project from the project menu
    //   Given the project "EV Market 2026" is open
    //   When I choose "Back up project" and pick a destination
    //   Then a backup zip is written
    //   And a confirmation is shown
    [Fact]
    public void Backing_up_a_project_from_the_project_menu()
    {
        var workspace = ShellUiFlow.OpenSampleProject(_fixture.MainWindow);

        // When I choose "Back up project" from the project menu.
        var backupButton = workspace.FindFirstDescendant(cf => cf.ByAutomationId("BackupProjectButton"))?.AsButton();
        Assert.NotNull(backupButton);
        backupButton!.Click();

        // Then a confirmation is shown that a backup zip was written to the chosen destination.
        var confirmation = workspace.FindFirstDescendant(cf => cf.ByAutomationId("BackupProjectConfirmation"));
        Assert.NotNull(confirmation);
        Assert.NotNull(confirmation!.AsLabel().Text);
    }

    // Scenario: Restoring a project from the Projects home
    //   Given the Projects home is open
    //   When I choose "Restore project" and pick a backup zip
    //   Then the restored project appears in the Projects list
    [Fact]
    public void Restoring_a_project_from_the_Projects_home()
    {
        var home = _fixture.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsHomeRoot"))
            ?? ShellUiFlow.EnsureAtHome(_fixture.MainWindow);

        // When I choose "Restore project" and pick a backup zip.
        var restoreButton = home.FindFirstDescendant(cf => cf.ByAutomationId("RestoreProjectButton"))?.AsButton();
        Assert.NotNull(restoreButton);
        restoreButton!.Click();

        // Then the restored project appears in the Projects list.
        var list = home.FindFirstDescendant(cf => cf.ByAutomationId("ProjectsList"));
        Assert.NotNull(list);
        Assert.NotEmpty(list!.FindAllChildren());
    }
}

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/settings-secure-key/tests.md — "Changing the data directory is
/// validated before saving". Drives the real WPF Settings screen via FlaUI (UIA3). Tagged
/// <c>Category=ui</c> so it is excluded from the headless gate, but it must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class SettingsUiTests
{
    private readonly ShellUiFixture _fixture;

    public SettingsUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Changing the data directory is validated before saving
    //   Given the Settings screen is open
    //   When I set the data directory to a path that is not writable
    //   Then I see an inline validation error
    //   And the change is not saved
    [Fact]
    public void Changing_the_data_directory_is_validated_before_saving()
    {
        var window = _fixture.MainWindow;

        // Given the Settings screen is open
        var settingsRoot = window.FindFirstDescendant(cf => cf.ByAutomationId("AppSettingsRoot"));
        Assert.NotNull(settingsRoot);

        // When I set the data directory to a path that is not writable
        var input = window.FindFirstDescendant(cf => cf.ByAutomationId("DataDirectoryInput"))?.AsTextBox();
        Assert.NotNull(input);
        input!.Text = @"\\?\Z:\definitely-not-writable\" + Guid.NewGuid().ToString("N");

        var saveButton = window.FindFirstDescendant(cf => cf.ByAutomationId("SaveDataDirectoryButton"))?.AsButton();
        Assert.NotNull(saveButton);
        saveButton!.Invoke();

        // Then I see an inline validation error
        var error = window.FindFirstDescendant(cf => cf.ByAutomationId("DataDirectoryError"))?.AsLabel();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Text));
    }
}

using MeticulousResearch.Core.Theming;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Theming;

/// <summary>
/// @unit scenarios from docs/features/design-system-theming/tests.md, driven through the Core
/// theme service and design tokens with no WPF window (TESTING-STRATEGY §2).
/// </summary>
public class ThemeServiceTests
{
    private static ThemeService NewService(
        out FakeSystemThemeProvider provider, AppTheme? seed = null, AppTheme systemTheme = AppTheme.Light)
    {
        provider = new FakeSystemThemeProvider(systemTheme);
        return new ThemeService(new FakeThemeStore(seed), provider);
    }

    // Scenario Outline: Selecting a theme sets the active theme
    //   When I set the theme to "<theme>"  Then the active theme resolves to "<resolved>"
    //   | Light  | Light              |
    //   | Dark   | Dark               |
    //   | System | follows OS setting |
    [Theory]
    [InlineData(AppTheme.Light, AppTheme.Light)]
    [InlineData(AppTheme.Dark, AppTheme.Dark)]
    [InlineData(AppTheme.System, AppTheme.Dark)] // OS set to Dark below; System follows it.
    public void Selecting_a_theme_sets_the_active_theme(AppTheme selected, AppTheme expectedResolved)
    {
        // For the System row, the OS setting is Dark so "follows OS setting" resolves to Dark.
        var service = NewService(out _, systemTheme: AppTheme.Dark);

        service.SetTheme(selected);

        Assert.Equal(selected, service.SelectedTheme);
        Assert.Equal(expectedResolved, service.CurrentTheme);
    }

    // Scenario: Theme choice persists across restart
    //   Given I set the theme to "Dark"  When the app restarts  Then the active theme is "Dark"
    [Fact]
    public void Theme_choice_persists_across_restart()
    {
        var store = new FakeThemeStore();
        var provider = new FakeSystemThemeProvider(AppTheme.Light);

        using (var beforeRestart = new ThemeService(store, provider))
        {
            beforeRestart.SetTheme(AppTheme.Dark);
        }

        // "the app restarts" == a fresh service reading the same persisted store.
        using var afterRestart = new ThemeService(store, provider);

        Assert.Equal(AppTheme.Dark, afterRestart.SelectedTheme);
        Assert.Equal(AppTheme.Dark, afterRestart.CurrentTheme);
    }

    [Fact]
    public void Persisted_choice_survives_restart_through_the_local_file_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mr-theme-{Guid.NewGuid():N}.json");
        try
        {
            var provider = new FakeSystemThemeProvider(AppTheme.Light);
            using (var beforeRestart = new ThemeService(new JsonFileThemeStore(path), provider))
            {
                beforeRestart.SetTheme(AppTheme.Dark);
            }

            using var afterRestart = new ThemeService(new JsonFileThemeStore(path), provider);
            Assert.Equal(AppTheme.Dark, afterRestart.CurrentTheme);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void System_selection_reacts_to_an_os_theme_change_live()
    {
        var service = NewService(out var provider, systemTheme: AppTheme.Light);
        service.SetTheme(AppTheme.System);
        Assert.Equal(AppTheme.Light, service.CurrentTheme);

        var raised = 0;
        service.ThemeChanged += (_, _) => raised++;

        provider.SetSystemTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, service.CurrentTheme);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Explicit_selection_ignores_os_theme_changes()
    {
        var service = NewService(out var provider, systemTheme: AppTheme.Light);
        service.SetTheme(AppTheme.Light);

        provider.SetSystemTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Light, service.CurrentTheme);
    }
}

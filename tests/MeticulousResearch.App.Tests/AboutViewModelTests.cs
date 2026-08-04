using System.Reflection;
using MeticulousResearch.App.Tests.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.AppInfo;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the About screen (docs/features/about-screen/tests.md): the identity (product
/// name, app icon) and version scenarios. All state lives on <see cref="AboutViewModel"/> via
/// <see cref="IAppInfo"/> so it is asserted without a window.
/// </summary>
public sealed class AboutViewModelTests
{
    private static AboutViewModel CreateVm(IAppInfo appInfo) =>
        new(appInfo, TestNavigationServiceFactory.Create());

    // @unit
    // Scenario: The About screen shows the product name
    //   Given the About screen
    //   Then it displays the product name "MeticulousResearch Desktop"
    [Fact]
    public void About_screen_shows_the_product_name()
    {
        var vm = CreateVm(new AssemblyAppInfo(typeof(AboutViewModel).Assembly));

        Assert.Equal("MeticulousResearch Desktop", vm.ProductName);
    }

    // @unit
    // Scenario: The About screen shows the app icon
    //   Given the About screen
    //   Then it displays the application icon
    [Fact]
    public void About_screen_shows_the_app_icon()
    {
        var vm = CreateVm(new AssemblyAppInfo(typeof(AboutViewModel).Assembly));

        Assert.False(string.IsNullOrWhiteSpace(vm.IconResource));
    }

    // @unit
    // Scenario: The About screen shows the application version
    //   Given the running app reports a version
    //   When I open the About screen
    //   Then it displays that version
    [Fact]
    public void About_screen_shows_the_application_version()
    {
        var appInfo = new FakeAppInfo { Version = "1.0.0" };

        var vm = CreateVm(appInfo);

        Assert.Equal("1.0.0", vm.Version);
    }

    // @unit
    // Scenario: The displayed version comes from the assembly, not a hard-coded string
    //   Given the app's assembly version is "1.0.0"
    //   When I read the version shown on the About screen
    //   Then it equals the assembly's informational version
    [Fact]
    public void Displayed_version_comes_from_the_assembly_not_a_hard_coded_string()
    {
        var assembly = typeof(AboutViewModel).Assembly;
        var expected = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        var vm = CreateVm(new AssemblyAppInfo(assembly));

        Assert.Equal(expected, vm.Version);
    }

    private sealed class FakeAppInfo : IAppInfo
    {
        public string ProductName { get; init; } = "MeticulousResearch Desktop";
        public string Version { get; init; } = string.Empty;
        public string IconResource { get; init; } = AssemblyAppInfo.AppIconResourceKey;
    }
}

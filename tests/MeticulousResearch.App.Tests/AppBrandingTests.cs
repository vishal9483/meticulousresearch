using System.IO;
using System.Reflection;
using MeticulousResearch.App.Branding;
using MeticulousResearch.Core.AppInfo;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// <c>@unit</c> scenarios from docs/features/app-branding-icon/tests.md (SPEC §3.7). These verify the
/// shipped brand assets without a window: the multi-resolution application icon and the
/// single-source product name. The <c>@ui</c> window/onboarding scenarios live in UiTests and the
/// <c>@manual</c> taskbar/Start-Menu/installer/coherence scenarios are checklist tests below.
/// </summary>
public sealed class AppBrandingTests
{
    // @unit
    // Scenario: The app ships a multi-resolution application icon
    //   Given the app's icon asset
    //   Then it provides the standard Windows icon sizes (16, 32, 48, 256)
    //   And it is the icon referenced by the executable
    [Fact]
    public void Icon_asset_provides_the_standard_windows_sizes()
    {
        var icoPath = Path.Combine(AppProjectDir(), "Assets", "AppIcon.ico");
        Assert.True(File.Exists(icoPath), $"Icon asset missing at {icoPath}");

        var sizes = ReadIcoSizes(icoPath);

        Assert.Contains(16, sizes);
        Assert.Contains(32, sizes);
        Assert.Contains(48, sizes);
        Assert.Contains(256, sizes);
    }

    // @unit (second Then/And of the icon scenario): the executable references that same icon.
    [Fact]
    public void Icon_asset_is_the_icon_referenced_by_the_executable()
    {
        var csprojPath = Path.Combine(AppProjectDir(), "MeticulousResearch.App.csproj");
        var csproj = File.ReadAllText(csprojPath);

        // The <ApplicationIcon> the executable is built with points at the same asset the test parsed.
        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", csproj);
        Assert.Equal("Assets/AppIcon.ico", AppBranding.IconAssetPath);
    }

    // @unit
    // Scenario: The product name is defined once and reused
    //   Given the app's branding metadata
    //   Then the product name resolves to "MeticulousResearch Desktop"
    //   And the window title, About screen, and package metadata all read from that single source
    [Fact]
    public void Product_name_resolves_to_the_expected_display_name()
    {
        Assert.Equal("MeticulousResearch Desktop", AssemblyAppInfo.ProductNameValue);
    }

    [Fact]
    public void Window_title_reads_from_the_single_product_name_source()
    {
        // The window title carries the single-source product name (no duplicated literal).
        Assert.Equal(AssemblyAppInfo.ProductNameValue, AppBranding.ProductName);
        Assert.Equal(AssemblyAppInfo.ProductNameValue, AppBranding.WindowTitle);
    }

    [Fact]
    public void About_screen_reads_from_the_single_product_name_source()
    {
        // The About screen displays IAppInfo.ProductName, which is the same single source.
        IAppInfo appInfo = new AssemblyAppInfo(typeof(AppBranding).Assembly);
        Assert.Equal(AssemblyAppInfo.ProductNameValue, appInfo.ProductName);
    }

    [Fact]
    public void Package_metadata_reads_from_the_single_product_name_source()
    {
        // The package/assembly Product attribute is stamped from the same const, so metadata and UI
        // never diverge.
        var product = typeof(AppBranding).Assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        Assert.Equal(AssemblyAppInfo.ProductNameValue, product);
    }

    /// <summary>Parses an .ico ICONDIR and returns the pixel width of each contained image (256 for 0).</summary>
    private static IReadOnlyList<int> ReadIcoSizes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            int entry = 6 + 16 * i;
            int width = bytes[entry];
            sizes.Add(width == 0 ? 256 : width);
        }
        return sizes;
    }

    /// <summary>Walks up from the test output directory to the App project directory in the repo.</summary>
    private static string AppProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MeticulousResearch.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MeticulousResearch.App");
    }
}

using System.IO;
using System.Xml.Linq;
using MeticulousResearch.App.Branding;
using MeticulousResearch.Core.AppInfo;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// <c>@unit</c> scenario from docs/features/installer/tests.md (SPEC §8). Verifies — without a
/// window — that the packaged installer version is single-sourced: the MSIX manifest version, the
/// app assembly/product version, and the version the About screen reports all derive from
/// <c>installer/version.props</c> and agree.
/// </summary>
public sealed class InstallerTests
{
    // @unit
    // Scenario: The packaged version matches the assembly version
    //   Given the release manifest and the built assembly
    //   Then the installer package version equals the app's assembly/product version
    //   And that version is the one the About screen reports
    [Fact]
    public void Packaged_version_matches_assembly_and_about_screen_version()
    {
        var repoRoot = RepoRoot();

        // The single source of the version.
        var singleSource = ReadSingleSourceVersion(repoRoot);
        Assert.False(string.IsNullOrWhiteSpace(singleSource), "installer/version.props has no MeticulousVersion");

        // The installer package version, from the MSIX manifest (4-part; normalized to 3-part).
        var packageVersion = ReadManifestVersion(repoRoot);

        // The app's assembly/product version, which is exactly what the About screen displays.
        IAppInfo appInfo = new AssemblyAppInfo(typeof(AppBranding).Assembly);
        var aboutVersion = appInfo.Version;

        // Then the installer package version equals the app's assembly/product version...
        Assert.Equal(singleSource, packageVersion);
        Assert.Equal(singleSource, aboutVersion);
        // ...and (transitively) the About-screen version equals the packaged version.
        Assert.Equal(packageVersion, aboutVersion);
    }

    /// <summary>Reads the MeticulousVersion property (the single version source) from installer/version.props.</summary>
    private static string ReadSingleSourceVersion(string repoRoot)
    {
        var propsPath = Path.Combine(repoRoot, "installer", "version.props");
        Assert.True(File.Exists(propsPath), $"version.props missing at {propsPath}");
        var doc = XDocument.Load(propsPath);
        var value = doc.Descendants("MeticulousVersion").FirstOrDefault()?.Value.Trim();
        return value ?? string.Empty;
    }

    /// <summary>Reads the MSIX Identity/@Version and normalizes the 4-part value to 3 parts.</summary>
    private static string ReadManifestVersion(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "installer", "AppxManifest.xml");
        Assert.True(File.Exists(manifestPath), $"AppxManifest.xml missing at {manifestPath}");
        var doc = XDocument.Load(manifestPath);
        var identity = doc.Descendants().First(e => e.Name.LocalName == "Identity");
        var raw = identity.Attribute("Version")?.Value ?? string.Empty;

        var parts = raw.Split('.');
        return parts.Length >= 3 ? string.Join('.', parts[0], parts[1], parts[2]) : raw;
    }

    /// <summary>Walks up from the test output directory to the repository root (the solution folder).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MeticulousResearch.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}

using MeticulousResearch.Core.AppInfo;

namespace MeticulousResearch.App.Branding;

/// <summary>
/// The single source of the app's brand identity for WPF chrome (app-branding-icon/phase.md,
/// SPEC §3.7). The product name comes from Core's <see cref="AssemblyAppInfo.ProductNameValue"/> so
/// the window title, About screen, onboarding, and package metadata never duplicate the literal.
/// The application icon is the packaged <c>Assets/AppIcon.ico</c>, shared with the taskbar, Start
/// Menu, and installer.
/// </summary>
public static class AppBranding
{
    /// <summary>The product display name shown everywhere the app identifies itself.</summary>
    public static string ProductName => AssemblyAppInfo.ProductNameValue;

    /// <summary>The main-window title text (the product name — no default WPF chrome name).</summary>
    public static string WindowTitle => ProductName;

    /// <summary>The pack URI of the shared multi-resolution application icon.</summary>
    public const string IconPackUri = "pack://application:,,,/Assets/AppIcon.ico";

    /// <summary>The project-relative path of the icon asset the executable references.</summary>
    public const string IconAssetPath = "Assets/AppIcon.ico";
}

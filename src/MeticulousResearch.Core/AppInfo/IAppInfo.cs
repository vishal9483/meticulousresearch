namespace MeticulousResearch.Core.AppInfo;

/// <summary>
/// Exposes the running application's identity — product name, version, and app-icon resource
/// reference — so the About screen can display them and <c>@unit</c> tests can assert them without
/// a window (about-screen/phase.md, SPEC §3.7). The version is sourced from the assembly's
/// informational version rather than a hard-coded literal so it tracks the build.
/// </summary>
public interface IAppInfo
{
    /// <summary>The product name shown as the app's identity (e.g. "MeticulousResearch Desktop").</summary>
    string ProductName { get; }

    /// <summary>The application version, read from the assembly's informational version.</summary>
    string Version { get; }

    /// <summary>
    /// A reference to the shared application-icon resource (a WPF resource key). Until
    /// <c>app-branding-icon</c> (M6) supplies the final icon, this points at the placeholder brand
    /// asset from the design system so the About screen is never blank.
    /// </summary>
    string IconResource { get; }
}

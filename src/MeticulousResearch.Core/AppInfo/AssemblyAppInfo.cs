using System.Reflection;

namespace MeticulousResearch.Core.AppInfo;

/// <summary>
/// The default <see cref="IAppInfo"/>: reports a fixed product name and reads the version from a
/// supplied assembly's <see cref="AssemblyInformationalVersionAttribute"/> (falling back to the
/// assembly's file version) so the value tracks the build instead of being hard-coded
/// (about-screen/phase.md). Window-free so it is <c>@unit</c>-assertable.
/// </summary>
public sealed class AssemblyAppInfo : IAppInfo
{
    /// <summary>The product name shown on the About screen.</summary>
    public const string ProductNameValue = "MeticulousResearch Desktop";

    /// <summary>The WPF resource key for the shared application icon (placeholder brand asset for now).</summary>
    public const string AppIconResourceKey = "AppIcon";

    private readonly string _version;

    /// <summary>Creates app info that reads its version from <paramref name="assembly"/>.</summary>
    /// <param name="assembly">The assembly whose informational version identifies the running app.</param>
    public AssemblyAppInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _version = ReadVersion(assembly);
    }

    /// <inheritdoc />
    public string ProductName => ProductNameValue;

    /// <inheritdoc />
    public string Version => _version;

    /// <inheritdoc />
    public string IconResource => AppIconResourceKey;

    private static string ReadVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
            return informational!;

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}

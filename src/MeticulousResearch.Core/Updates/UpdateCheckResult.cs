namespace MeticulousResearch.Core.Updates;

/// <summary>
/// The outcome of an update check (update-notice/phase.md, SPEC §8): either the app is up to date
/// or a strictly-newer version is available. When available it carries the new version number and
/// is always modelled as a <b>non-blocking, dismissible</b> notice — never a modal interruption and
/// never a raw error, even on failure (which resolves to <see cref="UpToDate"/>).
/// </summary>
public sealed class UpdateCheckResult
{
    private UpdateCheckResult(bool isUpdateAvailable, string? newVersion)
    {
        IsUpdateAvailable = isUpdateAvailable;
        NewVersion = newVersion;
    }

    /// <summary>The shared "no update" result: no notice is raised.</summary>
    public static UpdateCheckResult UpToDate { get; } = new(false, null);

    /// <summary>Builds an "update available" notice for the given advertised <paramref name="version"/>.</summary>
    /// <param name="version">The advertised newer version to show in the notice.</param>
    public static UpdateCheckResult UpdateAvailable(string version) => new(true, version);

    /// <summary>Whether a newer version is available and a notice should be shown.</summary>
    public bool IsUpdateAvailable { get; }

    /// <summary>The advertised new version number when <see cref="IsUpdateAvailable"/>; otherwise <c>null</c>.</summary>
    public string? NewVersion { get; }

    /// <summary>The update notice is never modal — it must not interrupt work (SPEC §1.3, §3.7).</summary>
    public bool IsBlocking => false;

    /// <summary>The update notice can always be dismissed by the user (SPEC §8).</summary>
    public bool IsDismissible => IsUpdateAvailable;
}

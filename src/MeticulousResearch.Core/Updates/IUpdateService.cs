namespace MeticulousResearch.Core.Updates;

/// <summary>
/// Compares the current installed version against the latest advertised version and produces a
/// non-blocking update notice (update-notice/phase.md, SPEC §8). The check runs off the UI thread,
/// never blocks, and never surfaces a raw error: offline/failed checks resolve to
/// <see cref="UpdateCheckResult.UpToDate"/>. A version the user has dismissed is not re-notified
/// until a strictly-newer version appears.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for an update. Returns <see cref="UpdateCheckResult.UpdateAvailable"/> only when the
    /// latest advertised version is a valid version strictly newer than both the current version
    /// and any previously-dismissed version; otherwise <see cref="UpdateCheckResult.UpToDate"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers that the user dismissed the notice for <paramref name="version"/> so it is not
    /// raised again until a strictly-newer version is advertised.
    /// </summary>
    /// <param name="version">The advertised version whose notice was dismissed.</param>
    void Dismiss(string version);
}

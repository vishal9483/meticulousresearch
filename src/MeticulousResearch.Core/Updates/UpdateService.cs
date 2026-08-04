using MeticulousResearch.Core.AppInfo;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.Core.Updates;

/// <summary>
/// The default <see cref="IUpdateService"/> (update-notice/phase.md, SPEC §8). It reads the current
/// version from <see cref="IAppInfo"/> (the same single source the About screen and installer use),
/// fetches the latest advertised version from an injected <see cref="ILatestVersionProvider"/>, and
/// applies semantic-version comparison plus dismissal memory. All network work lives behind the
/// provider so this class is <c>@unit</c>-testable with no I/O. Failures never reach the UI: they
/// are logged off-screen (when a log is supplied) and resolved to
/// <see cref="UpdateCheckResult.UpToDate"/> (SPEC §7.5, §9.1(10)).
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IAppInfo _appInfo;
    private readonly ILatestVersionProvider _latestProvider;
    private readonly ISettingsService _settings;
    private readonly IErrorLog? _errorLog;

    /// <summary>Creates the update service over the app-info, latest-version provider, and settings.</summary>
    /// <param name="appInfo">Supplies the current installed version (the single version source).</param>
    /// <param name="latestProvider">Supplies the latest advertised version from the update source.</param>
    /// <param name="settings">Persists the dismissed version so it is not re-notified.</param>
    /// <param name="errorLog">Optional off-screen sink for check failures; never shown to the user.</param>
    public UpdateService(
        IAppInfo appInfo,
        ILatestVersionProvider latestProvider,
        ISettingsService settings,
        IErrorLog? errorLog = null)
    {
        _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
        _latestProvider = latestProvider ?? throw new ArgumentNullException(nameof(latestProvider));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _errorLog = errorLog;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        string? latestRaw;
        try
        {
            latestRaw = await _latestProvider.GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Offline / unreachable / bad response: swallow to "no notice", log off-screen only.
            _errorLog?.LogUnexpected("update-check", ex);
            return UpdateCheckResult.UpToDate;
        }

        if (!TryParseVersion(latestRaw, out var latest))
            return UpdateCheckResult.UpToDate;

        if (!TryParseVersion(_appInfo.Version, out var current))
            return UpdateCheckResult.UpToDate;

        if (latest <= current)
            return UpdateCheckResult.UpToDate;

        if (TryParseVersion(_settings.DismissedUpdateVersion, out var dismissed) && latest <= dismissed)
            return UpdateCheckResult.UpToDate;

        return UpdateCheckResult.UpdateAvailable(latestRaw!.Trim());
    }

    /// <inheritdoc />
    public void Dismiss(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return;

        _settings.DismissedUpdateVersion = version.Trim();
    }

    /// <summary>
    /// Parses a strict "major.minor[.build[.revision]]" version. Malformed, pre-release, or empty
    /// strings fail to parse and are treated as "no update" — never an error (SPEC §9.1(10)).
    /// </summary>
    private static bool TryParseVersion(string? value, out Version version)
    {
        if (!string.IsNullOrWhiteSpace(value) && Version.TryParse(value.Trim(), out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }
}

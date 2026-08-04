using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.AppInfo;
using MeticulousResearch.Core.Updates;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The About screen (about-screen/phase.md, SPEC §3.7): shows the app's identity — product name,
/// application icon, and assembly version — and closes back to the screen that opened it. All
/// testable state lives here via <see cref="IAppInfo"/> so it is <c>@unit</c>-assertable without a
/// window; the view is a trivial styled presentation of these values. The room below the version
/// hosts the non-blocking update notice (update-notice/phase.md, SPEC §8) when a newer version is
/// advertised.
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IAppInfo _appInfo;
    private readonly INavigationService _navigation;
    private readonly IUpdateService? _updateService;

    /// <summary>Creates the About view-model over the app-info contract and navigation service.</summary>
    /// <param name="appInfo">The running app's identity (product name, version, icon reference).</param>
    /// <param name="navigation">The shared navigation service used to close back to the prior screen.</param>
    /// <param name="updateService">
    /// Optional update-notice service (update-notice/phase.md). When supplied, the About screen
    /// surfaces a non-blocking "update available" notice. Optional so the About feature's own
    /// <c>@unit</c> tests can construct the view-model without it.
    /// </param>
    public AboutViewModel(IAppInfo appInfo, INavigationService navigation, IUpdateService? updateService = null)
    {
        _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _updateService = updateService;
    }

    /// <summary>The heading shown at the top of the About screen.</summary>
    public string Title => "About";

    /// <summary>The product name displayed as the app's identity.</summary>
    public string ProductName => _appInfo.ProductName;

    /// <summary>The application version, sourced from the assembly (not hard-coded).</summary>
    public string Version => _appInfo.Version;

    /// <summary>The resource key of the application icon shown on the screen.</summary>
    public string IconResource => _appInfo.IconResource;

    /// <summary>Whether the non-blocking "update available" notice is currently shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNoticeText))]
    private bool _isUpdateNoticeVisible;

    /// <summary>The advertised new version shown in the update notice, or <c>null</c> when none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNoticeText))]
    private string? _updateVersion;

    /// <summary>The human-readable update-notice message, e.g. "Update available: 1.0.1".</summary>
    public string UpdateNoticeText =>
        string.IsNullOrWhiteSpace(UpdateVersion) ? string.Empty : $"Update available: {UpdateVersion}";

    /// <summary>
    /// Runs the update check off the UI thread and reflects the result in the notice state. The UI
    /// never awaits this; failures resolve silently to "no notice" (SPEC §7.5, §9.1(10)).
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_updateService is null)
            return;

        var result = await _updateService.CheckForUpdatesAsync().ConfigureAwait(true);
        if (result.IsUpdateAvailable)
        {
            UpdateVersion = result.NewVersion;
            IsUpdateNoticeVisible = true;
        }
        else
        {
            IsUpdateNoticeVisible = false;
        }
    }

    /// <summary>Dismisses the update notice and remembers the version so it is not re-notified.</summary>
    [RelayCommand]
    public void DismissUpdateNotice()
    {
        if (!string.IsNullOrWhiteSpace(UpdateVersion))
            _updateService?.Dismiss(UpdateVersion!);

        IsUpdateNoticeVisible = false;
    }

    /// <summary>Closes the About screen, returning to the screen that opened it.</summary>
    [RelayCommand]
    public void Close() => _navigation.Back();
}

using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.AppInfo;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The About screen (about-screen/phase.md, SPEC §3.7): shows the app's identity — product name,
/// application icon, and assembly version — and closes back to the screen that opened it. All
/// testable state lives here via <see cref="IAppInfo"/> so it is <c>@unit</c>-assertable without a
/// window; the view is a trivial styled presentation of these values.
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IAppInfo _appInfo;
    private readonly INavigationService _navigation;

    /// <summary>Creates the About view-model over the app-info contract and navigation service.</summary>
    /// <param name="appInfo">The running app's identity (product name, version, icon reference).</param>
    /// <param name="navigation">The shared navigation service used to close back to the prior screen.</param>
    public AboutViewModel(IAppInfo appInfo, INavigationService navigation)
    {
        _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    /// <summary>The heading shown at the top of the About screen.</summary>
    public string Title => "About";

    /// <summary>The product name displayed as the app's identity.</summary>
    public string ProductName => _appInfo.ProductName;

    /// <summary>The application version, sourced from the assembly (not hard-coded).</summary>
    public string Version => _appInfo.Version;

    /// <summary>The resource key of the application icon shown on the screen.</summary>
    public string IconResource => _appInfo.IconResource;

    /// <summary>Closes the About screen, returning to the screen that opened it.</summary>
    [RelayCommand]
    public void Close() => _navigation.Back();
}

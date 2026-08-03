using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Onboarding;

namespace MeticulousResearch.App.Services;

/// <summary>
/// Decides, at launch, whether to run first-run onboarding (SPEC §3.8): when onboarding has not
/// been completed it shows the wizard starting at the Welcome step; otherwise the app opens
/// directly on the Projects home. Window-free so the launch decision is <c>@unit</c>-testable.
/// </summary>
public sealed class OnboardingCoordinator
{
    private readonly IOnboardingState _state;
    private readonly INavigationService _navigation;
    private readonly Func<OnboardingViewModel> _wizardFactory;

    /// <summary>Creates the coordinator over the onboarding state, navigation, and a wizard factory.</summary>
    public OnboardingCoordinator(
        IOnboardingState state,
        INavigationService navigation,
        Func<OnboardingViewModel> wizardFactory)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _wizardFactory = wizardFactory ?? throw new ArgumentNullException(nameof(wizardFactory));
    }

    /// <summary>True when onboarding should run (it has not yet been completed).</summary>
    public bool ShouldRunOnboarding => !_state.IsCompleted;

    /// <summary>The wizard currently being shown, or null when onboarding is not running.</summary>
    public OnboardingViewModel? ActiveWizard { get; private set; }

    /// <summary>True while the onboarding wizard is being shown.</summary>
    public bool IsShowingOnboarding => ActiveWizard is not null;

    /// <summary>
    /// Runs the launch decision: shows the onboarding wizard (starting at Welcome) on first run,
    /// otherwise navigates straight to the Projects home.
    /// </summary>
    public void RunAtLaunch()
    {
        if (ShouldRunOnboarding)
        {
            _state.CurrentStep = OnboardingStep.Welcome;
            ActiveWizard = _wizardFactory();
        }
        else
        {
            ActiveWizard = null;
            _navigation.NavigateTo<ProjectsHomeViewModel>();
        }
    }
}

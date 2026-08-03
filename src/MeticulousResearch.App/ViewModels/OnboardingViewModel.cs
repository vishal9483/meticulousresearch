using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Security;
using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The first-run onboarding wizard view-model (SPEC §3.8): a stepper through Welcome → API key →
/// Defaults → Sample project → Done, with Next/Back/Skip. All step logic — the key-step advance
/// gate, defaults pre-fill/persist, sample-project opt-in, and finish/skip completion — lives here
/// so it is <c>@unit</c>-testable without a window. Key handling is delegated to
/// <see cref="ISecureKeyStore"/>/<see cref="IKeyTester"/>; the entered key is stored securely and
/// never written to settings.
/// </summary>
public sealed partial class OnboardingViewModel : ViewModelBase
{
    private readonly IOnboardingState _state;
    private readonly ISecureKeyStore _keyStore;
    private readonly IApiCredentialProvider _credentials;
    private readonly IKeyTester _keyTester;
    private readonly ISettingsService _settings;
    private readonly IDataDirectoryValidator _dataDirectoryValidator;
    private readonly ISampleProjectFactory _sampleProjectFactory;
    private readonly INavigationService _navigation;
    private readonly IFirstRunHints _hints;

    private bool _keyValidated;

    /// <summary>Creates the wizard over its Core/App services, pre-filling defaults from settings.</summary>
    /// <param name="state">The persisted onboarding state (completed flag + current step).</param>
    /// <param name="keyStore">Secure key vault (the entered key is saved here, never in plaintext).</param>
    /// <param name="credentials">Effective-credential resolver (detects an env-provided key).</param>
    /// <param name="keyTester">Validates a key against the Models endpoint (mocked in tests).</param>
    /// <param name="settings">App settings for defaults pre-fill/persist.</param>
    /// <param name="dataDirectoryValidator">Validates the chosen data directory is writable.</param>
    /// <param name="sampleProjectFactory">Builds the optional bundled sample project.</param>
    /// <param name="navigation">Navigation used to land on the Projects home on finish/skip.</param>
    /// <param name="hints">First-run hint state requested on finish.</param>
    /// <param name="defaultDataDirectory">The default data-directory location used to pre-fill the field.</param>
    public OnboardingViewModel(
        IOnboardingState state,
        ISecureKeyStore keyStore,
        IApiCredentialProvider credentials,
        IKeyTester keyTester,
        ISettingsService settings,
        IDataDirectoryValidator dataDirectoryValidator,
        ISampleProjectFactory sampleProjectFactory,
        INavigationService navigation,
        IFirstRunHints hints,
        string defaultDataDirectory)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _keyTester = keyTester ?? throw new ArgumentNullException(nameof(keyTester));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dataDirectoryValidator = dataDirectoryValidator ?? throw new ArgumentNullException(nameof(dataDirectoryValidator));
        _sampleProjectFactory = sampleProjectFactory ?? throw new ArgumentNullException(nameof(sampleProjectFactory));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _hints = hints ?? throw new ArgumentNullException(nameof(hints));

        _currentStep = _state.CurrentStep;

        // Defaults pre-fill (SPEC §3.8(3)): sensible defaults from settings; data dir falls back
        // to the default location when unset.
        DefaultModel = _settings.DefaultModel;
        Theme = _settings.Theme;
        DataDirectory = string.IsNullOrWhiteSpace(_settings.DataDirectory)
            ? defaultDataDirectory
            : _settings.DataDirectory!;
    }

    // ----- Stepper -----

    /// <summary>The step the wizard is currently on.</summary>
    [ObservableProperty]
    private OnboardingStep _currentStep;

    partial void OnCurrentStepChanged(OnboardingStep value) => _state.CurrentStep = value;

    /// <summary>True when a Back navigation is possible (not on the first step).</summary>
    public bool CanGoBack => CurrentStep > OnboardingStep.Welcome;

    /// <summary>True when the final "Done" step is showing (Finish is offered instead of Next).</summary>
    public bool IsFinalStep => CurrentStep == OnboardingStep.Done;

    /// <summary>
    /// Whether the wizard may advance from the current step. Every step is freely advanceable
    /// except the API key step, which requires a validated key or an environment-provided key
    /// (advance is otherwise blocked until the user validates a key or skips).
    /// </summary>
    public bool CanContinue => CurrentStep != OnboardingStep.ApiKey || CanAdvanceFromKeyStep;

    /// <summary>Advances to the next step, applying that step's on-leave persistence/validation.</summary>
    [RelayCommand]
    public void Next() => TryAdvance();

    /// <summary>
    /// Core of <see cref="Next"/>, exposed for testing: advances when the current step allows it
    /// (persisting defaults / saving the key as appropriate) and returns whether it advanced.
    /// </summary>
    public bool TryAdvance()
    {
        switch (CurrentStep)
        {
            case OnboardingStep.ApiKey:
                if (!CanAdvanceFromKeyStep)
                    return false;
                // Persist the validated, user-entered key to the secure vault (never to settings).
                if (!IsKeyFromEnvironment && _keyValidated && !string.IsNullOrEmpty(ApiKeyInput))
                    _keyStore.Save(ApiKeyInput);
                break;

            case OnboardingStep.Defaults:
                if (!PersistDefaults())
                    return false;
                break;
        }

        if (CurrentStep < OnboardingStep.Done)
            CurrentStep = CurrentStep + 1;
        RaiseStepState();
        return true;
    }

    /// <summary>Returns to the previous step.</summary>
    [RelayCommand]
    public void Back()
    {
        if (CanGoBack)
        {
            CurrentStep = CurrentStep - 1;
            RaiseStepState();
        }
    }

    /// <summary>
    /// Skips onboarding from any step: marks it complete and lands on the Projects home
    /// (SPEC §3.8 — skippable at any step).
    /// </summary>
    [RelayCommand]
    public void Skip()
    {
        _state.MarkCompleted();
        _navigation.NavigateTo<ProjectsHomeViewModel>();
    }

    /// <summary>
    /// Finishes onboarding on the final step: marks it complete, requests first-run hints, and
    /// lands on the Projects home (SPEC §3.8(5)).
    /// </summary>
    [RelayCommand]
    public void Finish()
    {
        _state.MarkCompleted();
        _hints.Request();
        _navigation.NavigateTo<ProjectsHomeViewModel>();
    }

    // ----- Welcome step -----

    /// <summary>The privacy posture shown on the Welcome step (local-first).</summary>
    public string PrivacyStatement =>
        "Your data stays on this machine. MeticulousResearch is local-first — projects, resources, "
        + "and generated documents live in your data directory, not in the cloud.";

    /// <summary>Where the data directory lives (shown on the Welcome step).</summary>
    public string DataLocationDescription => $"Your data is stored in: {DataDirectory}";

    // ----- API key step -----

    /// <summary>The key the user is entering (bound to a masked field).</summary>
    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    /// <summary>The result message from the most recent "Test key" action.</summary>
    [ObservableProperty]
    private string? _testStatusMessage;

    /// <summary>True/false when the last test succeeded/failed; null when it was not run.</summary>
    [ObservableProperty]
    private bool? _testSucceeded;

    /// <summary>The models returned by the last successful test.</summary>
    public ObservableCollection<string> Models { get; } = new();

    /// <summary>True when the effective key is supplied by the <c>ANTHROPIC_API_KEY</c> environment variable.</summary>
    public bool IsKeyFromEnvironment => _credentials.IsApiKeyFromEnvironment;

    /// <summary>A notice shown on the key step when the environment already provides a key.</summary>
    public string? EnvironmentKeyNotice => IsKeyFromEnvironment
        ? "A key is already provided by the ANTHROPIC_API_KEY environment variable. You can continue."
        : null;

    /// <summary>
    /// Whether the key step is satisfied: an environment-provided key, or a key the user has
    /// tested successfully in this session.
    /// </summary>
    public bool CanAdvanceFromKeyStep => IsKeyFromEnvironment || _keyValidated;

    /// <summary>
    /// Tests the entered key: saves it to the secure vault so resolution picks it up, calls the
    /// key tester, and surfaces a success confirmation with the model list or an actionable error.
    /// An invalid key is removed from the vault and does not satisfy the advance gate.
    /// </summary>
    [RelayCommand]
    public async Task TestKeyAsync()
    {
        Models.Clear();

        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            _keyValidated = false;
            TestSucceeded = false;
            TestStatusMessage = "Enter a key before testing.";
            RaiseStepState();
            return;
        }

        _keyStore.Save(ApiKeyInput);
        var result = await _keyTester.TestAsync().ConfigureAwait(true);

        if (result.Success)
        {
            foreach (var model in result.Models)
                Models.Add(model);
            _keyValidated = true;
            TestSucceeded = true;
            TestStatusMessage = "Key verified. Available models loaded.";
        }
        else
        {
            // Do not keep an unvalidated key in the vault.
            _keyStore.Clear();
            _keyValidated = false;
            TestSucceeded = false;
            TestStatusMessage = result.ErrorMessage;
        }

        RaiseStepState();
    }

    // ----- Defaults step -----

    /// <summary>The default model tier (pre-filled to the app default, e.g. Claude Opus 5).</summary>
    [ObservableProperty]
    private string _defaultModel = string.Empty;

    /// <summary>The selected theme (pre-filled to "System").</summary>
    [ObservableProperty]
    private string _theme = string.Empty;

    /// <summary>The data directory (pre-filled to the default location).</summary>
    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    /// <summary>An inline validation error for the data directory, or null when valid.</summary>
    [ObservableProperty]
    private string? _dataDirectoryError;

    partial void OnDataDirectoryChanged(string value) => OnPropertyChanged(nameof(DataLocationDescription));

    private bool PersistDefaults()
    {
        if (!_dataDirectoryValidator.IsWritable(DataDirectory))
        {
            DataDirectoryError = "That folder can't be written to. Choose a writable location.";
            return false;
        }

        DataDirectoryError = null;
        _settings.DefaultModel = DefaultModel;
        _settings.Theme = Theme;
        _settings.DataDirectory = DataDirectory;
        return true;
    }

    // ----- Sample project step -----

    /// <summary>The id of the sample project created via opt-in, or null when none was created.</summary>
    public string? SampleProjectId { get; private set; }

    /// <summary>
    /// Creates the bundled sample project (a couple of resources + an example Market Research
    /// Report artifact) from bundled content — no network call, no key required — and advances.
    /// </summary>
    [RelayCommand]
    public void CreateSampleProject()
    {
        var project = _sampleProjectFactory.CreateSampleProject();
        SampleProjectId = project.Id;
        TryAdvance();
    }

    /// <summary>Declines the sample project (creates nothing) and advances.</summary>
    [RelayCommand]
    public void DeclineSampleProject() => TryAdvance();

    private void RaiseStepState()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsFinalStep));
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(CanAdvanceFromKeyStep));
        OnPropertyChanged(nameof(EnvironmentKeyNotice));
        OnPropertyChanged(nameof(IsKeyFromEnvironment));
    }
}

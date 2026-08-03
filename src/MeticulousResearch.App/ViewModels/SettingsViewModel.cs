using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Security;
using MeticulousResearch.Core.Settings;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// App-level Settings screen (settings-secure-key/phase.md, SPEC §3.5): masked API-key entry with
/// a Test button, API base URL with an environment-override indicator, default model, theme,
/// context budget, telemetry toggle, and a data-directory picker that validates writability before
/// saving. All rules live here so they are <c>@unit</c>-testable without a window.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISecureKeyStore _keyStore;
    private readonly IApiCredentialProvider _credentials;
    private readonly ISettingsService _settings;
    private readonly IKeyTester _keyTester;
    private readonly IDataDirectoryValidator _dataDirectoryValidator;
    private readonly IOnboardingState? _onboardingState;

    /// <summary>Creates the Settings view-model over its Core services.</summary>
    public SettingsViewModel(
        ISecureKeyStore keyStore,
        IApiCredentialProvider credentials,
        ISettingsService settings,
        IKeyTester keyTester,
        IDataDirectoryValidator dataDirectoryValidator,
        IOnboardingState? onboardingState = null)
    {
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyTester = keyTester ?? throw new ArgumentNullException(nameof(keyTester));
        _dataDirectoryValidator = dataDirectoryValidator ?? throw new ArgumentNullException(nameof(dataDirectoryValidator));
        _onboardingState = onboardingState;

        // Base URL: when the environment overrides it, show the effective value read-only.
        IsBaseUrlEnvironmentProvided = _credentials.IsBaseUrlFromEnvironment;
        BaseUrl = IsBaseUrlEnvironmentProvided
            ? _credentials.ResolveBaseUrl()
            : _settings.ApiBaseUrl ?? string.Empty;

        DefaultModel = _settings.DefaultModel;
        Theme = _settings.Theme;
        ContextBudget = _settings.ContextBudget;
        TelemetryEnabled = _settings.TelemetryEnabled;
        DataDirectory = _settings.DataDirectory ?? string.Empty;
    }

    /// <summary>The heading shown at the top of the Settings screen.</summary>
    public string Title => "Settings";

    // --- API key ---

    /// <summary>The key the user is entering (bound to a masked field in the view).</summary>
    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    /// <summary>True when a key is stored in the secure vault.</summary>
    public bool HasStoredKey => _keyStore.HasKey;

    /// <summary>The result message from the most recent "Test key" action.</summary>
    [ObservableProperty]
    private string? _testStatusMessage;

    /// <summary>True when the last test succeeded; false/null when it failed or was not run.</summary>
    [ObservableProperty]
    private bool? _testSucceeded;

    /// <summary>The models returned by the last successful test.</summary>
    public ObservableCollection<string> Models { get; } = new();

    /// <summary>Saves the entered key to the secure store (never to SQLite or a settings file).</summary>
    [RelayCommand]
    public void SaveKey()
    {
        if (!string.IsNullOrEmpty(ApiKeyInput))
        {
            _keyStore.Save(ApiKeyInput);
            OnPropertyChanged(nameof(HasStoredKey));
        }
    }

    /// <summary>Clears the stored key from the secure store.</summary>
    [RelayCommand]
    public void ClearKey()
    {
        _keyStore.Clear();
        OnPropertyChanged(nameof(HasStoredKey));
    }

    /// <summary>
    /// Tests connectivity by calling the Models endpoint at the resolved base URL with the
    /// resolved key, then surfaces a success confirmation + model list or an actionable error.
    /// </summary>
    [RelayCommand]
    public async Task TestKeyAsync()
    {
        var result = await _keyTester.TestAsync().ConfigureAwait(true);
        TestSucceeded = result.Success;
        Models.Clear();

        if (result.Success)
        {
            foreach (var model in result.Models)
                Models.Add(model);
            TestStatusMessage = "Key verified. Available models loaded.";
        }
        else
        {
            TestStatusMessage = result.ErrorMessage;
        }
    }

    // --- Base URL ---

    /// <summary>The base URL text shown in the field (effective env value when overridden).</summary>
    [ObservableProperty]
    private string _baseUrl = string.Empty;

    /// <summary>True when <c>ANTHROPIC_BASE_URL</c> is controlling the base URL (field is read-only).</summary>
    public bool IsBaseUrlEnvironmentProvided { get; }

    /// <summary>The persisted base-URL setting, or <c>null</c> when unset (env values are never persisted).</summary>
    public string? PersistedBaseUrl => _settings.ApiBaseUrl;

    /// <summary>Persists the base URL. Ignored when the environment is in control.</summary>
    [RelayCommand]
    public void SaveBaseUrl()
    {
        if (IsBaseUrlEnvironmentProvided)
            return;
        _settings.ApiBaseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? null : BaseUrl.Trim();
        OnPropertyChanged(nameof(PersistedBaseUrl));
    }

    // --- Preferences ---

    /// <summary>The default model for new conversations.</summary>
    [ObservableProperty]
    private string _defaultModel = string.Empty;

    partial void OnDefaultModelChanged(string value) => _settings.DefaultModel = value;

    /// <summary>The selected theme name.</summary>
    [ObservableProperty]
    private string _theme = string.Empty;

    partial void OnThemeChanged(string value) => _settings.Theme = value;

    /// <summary>The context budget in tokens.</summary>
    [ObservableProperty]
    private int _contextBudget;

    partial void OnContextBudgetChanged(int value) => _settings.ContextBudget = value;

    /// <summary>Whether telemetry is enabled (off by default).</summary>
    [ObservableProperty]
    private bool _telemetryEnabled;

    partial void OnTelemetryEnabledChanged(bool value) => _settings.TelemetryEnabled = value;

    // --- Data directory ---

    /// <summary>The configured data directory path.</summary>
    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    /// <summary>An inline validation error for the data directory, or <c>null</c> when valid.</summary>
    [ObservableProperty]
    private string? _dataDirectoryError;

    /// <summary>
    /// Validates the data directory is writable and, only then, persists it. On failure it sets an
    /// inline validation error and does not save (settings-secure-key data-directory scenario).
    /// </summary>
    [RelayCommand]
    public void SaveDataDirectory() => TrySaveDataDirectory();

    /// <summary>
    /// Core of <see cref="SaveDataDirectory"/> exposed for testing: returns true when the directory
    /// was validated and saved; false (with an inline error set) when it is not writable.
    /// </summary>
    public bool TrySaveDataDirectory()
    {
        if (!_dataDirectoryValidator.IsWritable(DataDirectory))
        {
            DataDirectoryError = "That folder can't be written to. Choose a writable location.";
            return false;
        }

        DataDirectoryError = null;
        _settings.DataDirectory = DataDirectory;
        return true;
    }

    // --- Re-run onboarding ---

    /// <summary>Raised when the user asks to re-run first-run onboarding, so the shell can relaunch it.</summary>
    public event EventHandler? RerunOnboardingRequested;

    /// <summary>
    /// Clears the onboarding completed flag and step and asks the shell to relaunch the wizard from
    /// the Welcome step (onboarding/phase.md — re-run entry point, SPEC §3.8).
    /// </summary>
    [RelayCommand]
    public void RerunOnboarding()
    {
        _onboardingState?.Reset();
        RerunOnboardingRequested?.Invoke(this, EventArgs.Empty);
    }
}

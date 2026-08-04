using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for <see cref="SettingsViewModel"/> covering the base-URL env-override indicator
/// (docs/features/settings-secure-key/tests.md — "A base URL supplied via the environment is
/// shown as an override and not persisted").
/// </summary>
public sealed class SettingsViewModelTests
{
    // @unit
    // Scenario: A base URL supplied via the environment is shown as an override and not persisted
    //   Given no API base URL setting has been saved
    //   And the environment variable "ANTHROPIC_BASE_URL" is set to "https://llm.sdc.siemens.cloud"
    //   When the Settings screen is opened
    //   Then the base URL field shows "https://llm.sdc.siemens.cloud" as environment-provided
    //   And the persisted base URL setting remains unset
    [Fact]
    public void Env_base_url_is_shown_as_override_and_not_persisted()
    {
        var settings = new InMemorySettings { ApiBaseUrl = null };
        var env = new FakeEnvironment().Set("ANTHROPIC_BASE_URL", "https://llm.sdc.siemens.cloud");
        var keyStore = new FakeSecureKeyStore();
        var credentials = new ApiCredentialProvider(env, keyStore, settings);

        // When the Settings screen is opened
        var vm = new SettingsViewModel(
            keyStore, credentials, settings,
            new StubKeyTester(), new StubDirectoryValidator(writable: true));

        // the base URL field shows the env value, marked environment-provided
        Assert.Equal("https://llm.sdc.siemens.cloud", vm.BaseUrl);
        Assert.True(vm.IsBaseUrlEnvironmentProvided);

        // the persisted base URL setting remains unset
        Assert.Null(settings.ApiBaseUrl);
        Assert.Null(vm.PersistedBaseUrl);
    }

    private sealed class InMemorySettings : ISettingsService
    {
        public string DefaultModel { get; set; } = SettingsService.DefaultModelValue;
        public string Theme { get; set; } = SettingsService.DefaultThemeValue;
        public int ContextBudget { get; set; } = SettingsService.DefaultContextBudgetValue;
        public bool TelemetryEnabled { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? DataDirectory { get; set; }
        public string? DismissedUpdateVersion { get; set; }
        public string ChatBackend { get; set; } = SettingsService.DefaultChatBackendValue;
        public event EventHandler? SettingsChanged;
        public void Raise() => SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class StubKeyTester : IKeyTester
    {
        public Task<KeyTestResult> TestAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(KeyTestResult.Ok(Array.Empty<string>()));
    }

    private sealed class StubDirectoryValidator : IDataDirectoryValidator
    {
        private readonly bool _writable;
        public StubDirectoryValidator(bool writable) => _writable = writable;
        public bool IsWritable(string path) => _writable;
    }
}

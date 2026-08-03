using MeticulousResearch.App.Services;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests.Onboarding;

/// <summary>
/// Faithful <c>@unit</c> translation of every non-<c>@ui</c>/<c>@manual</c> scenario in
/// docs/features/onboarding/tests.md (SPEC §3.8). Drives the window-free
/// <see cref="OnboardingViewModel"/> and <see cref="OnboardingCoordinator"/> over in-memory doubles.
/// </summary>
public sealed class OnboardingViewModelTests
{
    private const string DefaultDataDir = @"C:\Users\test\AppData\Local\MeticulousResearch";

    private sealed record Harness(
        OnboardingViewModel Vm,
        FakeOnboardingState State,
        FakeSecureKeyStore KeyStore,
        ConfigurableKeyTester KeyTester,
        OnboardingInMemorySettings Settings,
        RecordingSampleProjectFactory SampleFactory,
        RecordingNavigationService Navigation,
        FirstRunHints Hints,
        FakeEnvironment Environment);

    private static Harness NewVm(bool writableDataDir = true, Action<FakeEnvironment>? configureEnv = null)
    {
        var state = new FakeOnboardingState();
        var keyStore = new FakeSecureKeyStore();
        var settings = new OnboardingInMemorySettings();
        var env = new FakeEnvironment();
        configureEnv?.Invoke(env);
        var credentials = new ApiCredentialProvider(env, keyStore, settings);
        var keyTester = new ConfigurableKeyTester();
        var sampleFactory = new RecordingSampleProjectFactory();
        var navigation = new RecordingNavigationService();
        var hints = new FirstRunHints();

        var vm = new OnboardingViewModel(
            state, keyStore, credentials, keyTester, settings,
            new StubDirectoryValidator(writableDataDir), sampleFactory, navigation, hints, DefaultDataDir);

        return new Harness(vm, state, keyStore, keyTester, settings, sampleFactory, navigation, hints, env);
    }

    // ----- First-run trigger -----

    // Scenario: Onboarding runs on first launch
    //   Given a fresh installation with no completed onboarding
    //   When the app launches
    //   Then onboarding is shown starting at the Welcome step
    [Fact]
    public void Onboarding_runs_on_first_launch_at_welcome()
    {
        var h = NewVm();
        var coordinator = new OnboardingCoordinator(h.State, h.Navigation, () => h.Vm);

        coordinator.RunAtLaunch();

        Assert.True(coordinator.IsShowingOnboarding);
        Assert.NotNull(coordinator.ActiveWizard);
        Assert.Equal(OnboardingStep.Welcome, coordinator.ActiveWizard!.CurrentStep);
    }

    // Scenario: Onboarding does not run again after completion
    //   Given onboarding has been completed
    //   When the app launches
    //   Then onboarding is not shown
    //   And the app opens on the Projects home
    [Fact]
    public void Onboarding_does_not_run_after_completion_and_opens_projects_home()
    {
        var h = NewVm();
        h.State.MarkCompleted();
        var coordinator = new OnboardingCoordinator(h.State, h.Navigation, () => h.Vm);

        coordinator.RunAtLaunch();

        Assert.False(coordinator.IsShowingOnboarding);
        Assert.Null(coordinator.ActiveWizard);
        Assert.Equal(typeof(ProjectsHomeViewModel), h.Navigation.LastNavigatedTo);
    }

    // ----- API key step -----

    // @unit @requires-key
    // Scenario: A valid key can be tested and stored during onboarding
    //   Given onboarding is on the API key step
    //   And a mocked API that returns a model list for a valid key
    //   When I enter a key and click "Test key"
    //   Then I see a success confirmation with the available models
    //   And on continuing, the key is saved via the secure key store
    [Fact]
    [Trait("requires-key", "true")]
    public async Task Valid_key_can_be_tested_and_stored()
    {
        var h = NewVm();
        h.KeyTester.Returns(KeyTestResult.Ok(new[] { "claude-opus-5", "claude-sonnet-5" }));
        h.Vm.CurrentStep = OnboardingStep.ApiKey;
        h.Vm.ApiKeyInput = "sk-valid-123";

        await h.Vm.TestKeyAsync();

        // a success confirmation with the available models
        Assert.True(h.Vm.TestSucceeded);
        Assert.False(string.IsNullOrWhiteSpace(h.Vm.TestStatusMessage));
        Assert.Equal(new[] { "claude-opus-5", "claude-sonnet-5" }, h.Vm.Models);

        // on continuing, the key is saved via the secure key store
        Assert.True(h.Vm.TryAdvance());
        Assert.Equal("sk-valid-123", h.KeyStore.Get());
    }

    // Scenario: An invalid key shows an actionable error and blocks continue
    //   Given onboarding is on the API key step
    //   And a mocked API that returns 401 Unauthorized
    //   When I enter a key and click "Test key"
    //   Then I see a human-readable "key is invalid" error
    //   And no raw stack trace is shown
    //   And I cannot advance until a key is validated or I skip
    [Fact]
    public async Task Invalid_key_shows_actionable_error_and_blocks_continue()
    {
        var h = NewVm();
        h.KeyTester.Returns(KeyTestResult.Failure("Your API key is invalid. Check the key and try again."));
        h.Vm.CurrentStep = OnboardingStep.ApiKey;
        h.Vm.ApiKeyInput = "sk-bad";

        await h.Vm.TestKeyAsync();

        // a human-readable "key is invalid" error
        Assert.False(h.Vm.TestSucceeded);
        Assert.Contains("invalid", h.Vm.TestStatusMessage, StringComparison.OrdinalIgnoreCase);

        // no raw stack trace is shown
        Assert.DoesNotContain("Exception", h.Vm.TestStatusMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", h.Vm.TestStatusMessage!, StringComparison.Ordinal);

        // I cannot advance until a key is validated or I skip
        Assert.False(h.Vm.CanContinue);
        Assert.False(h.Vm.TryAdvance());
        Assert.Equal(OnboardingStep.ApiKey, h.Vm.CurrentStep);
    }

    // Scenario: The key step is pre-satisfied when the key comes from the environment
    //   Given onboarding is on the API key step
    //   And the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
    //   Then the step indicates a key is already provided by the environment
    //   And I can continue without entering or storing a key
    //   And "sk-from-env" is not written to the secure key store or any settings file
    [Fact]
    public void Key_step_is_pre_satisfied_from_the_environment()
    {
        var h = NewVm(configureEnv: env => env.Set("ANTHROPIC_API_KEY", "sk-from-env"));
        h.Vm.CurrentStep = OnboardingStep.ApiKey;

        // the step indicates a key is already provided by the environment
        Assert.True(h.Vm.IsKeyFromEnvironment);
        Assert.False(string.IsNullOrWhiteSpace(h.Vm.EnvironmentKeyNotice));

        // I can continue without entering or storing a key
        Assert.True(h.Vm.CanAdvanceFromKeyStep);
        Assert.Equal(string.Empty, h.Vm.ApiKeyInput);
        Assert.True(h.Vm.TryAdvance());

        // "sk-from-env" is not written to the secure key store or any settings file
        Assert.False(h.KeyStore.HasKey);
        Assert.Null(h.KeyStore.Get());
        Assert.DoesNotContain("sk-from-env", new[]
        {
            h.Settings.ApiBaseUrl, h.Settings.DataDirectory, h.Settings.DefaultModel, h.Settings.Theme,
        });
    }

    // ----- Defaults step -----

    // Scenario: The defaults step pre-fills sensible defaults
    //   Then the default model tier is pre-filled to Claude Opus 5
    //   And the theme is pre-filled to "System"
    //   And the data directory is pre-filled to the default location
    [Fact]
    public void Defaults_step_prefills_sensible_defaults()
    {
        var h = NewVm();

        Assert.Equal("claude-opus-5", h.Vm.DefaultModel);
        Assert.Equal("System", h.Vm.Theme);
        Assert.Equal(DefaultDataDir, h.Vm.DataDirectory);
    }

    // Scenario Outline: Choosing defaults persists them to settings
    //   When I set "<setting>" to "<value>" and continue
    //   Then "<setting>" is saved to settings as "<value>"
    [Theory]
    [InlineData("default model", "claude-sonnet-5")]
    [InlineData("theme", "Dark")]
    [InlineData("data directory", "a writable chosen path")]
    public void Choosing_defaults_persists_them_to_settings(string setting, string value)
    {
        var h = NewVm();
        h.Vm.CurrentStep = OnboardingStep.Defaults;

        string expected = value;
        switch (setting)
        {
            case "default model":
                h.Vm.DefaultModel = value;
                break;
            case "theme":
                h.Vm.Theme = value;
                break;
            case "data directory":
                expected = Path.Combine(Path.GetTempPath(), "mr-onboarding-dir", Guid.NewGuid().ToString("N"));
                h.Vm.DataDirectory = expected;
                break;
        }

        Assert.True(h.Vm.TryAdvance());

        switch (setting)
        {
            case "default model":
                Assert.Equal(expected, h.Settings.DefaultModel);
                break;
            case "theme":
                Assert.Equal(expected, h.Settings.Theme);
                break;
            case "data directory":
                Assert.Equal(expected, h.Settings.DataDirectory);
                break;
        }
    }

    // ----- Sample project (optional) -----

    // Scenario: Opting in creates a populated sample research project
    //   When I choose to create the sample project
    //   Then a sample project exists (the wizard delegates to the sample-project factory)
    [Fact]
    public void Opting_in_creates_the_sample_project_via_the_factory()
    {
        var h = NewVm();
        h.Vm.CurrentStep = OnboardingStep.SampleProject;

        h.Vm.CreateSampleProjectCommand.Execute(null);

        Assert.Equal(1, h.SampleFactory.CreateCount);
        Assert.False(string.IsNullOrEmpty(h.Vm.SampleProjectId));
    }

    // Scenario: Declining the sample project creates nothing
    //   When I decline the sample project
    //   Then no sample project is created
    [Fact]
    public void Declining_the_sample_project_creates_nothing()
    {
        var h = NewVm();
        h.Vm.CurrentStep = OnboardingStep.SampleProject;

        h.Vm.DeclineSampleProjectCommand.Execute(null);

        Assert.Equal(0, h.SampleFactory.CreateCount);
        Assert.Null(h.Vm.SampleProjectId);
    }

    // ----- Finish, skip -----

    // Scenario: Completing onboarding marks it done and lands on Projects home
    //   Given I am on the final step
    //   When I finish onboarding
    //   Then onboarding is marked complete
    //   And the app shows the Projects home
    //   And contextual hints on the primary actions are shown
    [Fact]
    public void Completing_onboarding_marks_done_lands_home_and_shows_hints()
    {
        var h = NewVm();
        h.Vm.CurrentStep = OnboardingStep.Done;

        h.Vm.FinishCommand.Execute(null);

        Assert.True(h.State.IsCompleted);
        Assert.Equal(typeof(ProjectsHomeViewModel), h.Navigation.LastNavigatedTo);
        Assert.True(h.Hints.ArePending);
    }

    // Scenario: Onboarding is skippable at any step
    //   Given onboarding is on any step
    //   When I choose "Skip"
    //   Then onboarding is marked complete
    //   And the app shows the Projects home
    [Theory]
    [InlineData(OnboardingStep.Welcome)]
    [InlineData(OnboardingStep.ApiKey)]
    [InlineData(OnboardingStep.Defaults)]
    [InlineData(OnboardingStep.SampleProject)]
    [InlineData(OnboardingStep.Done)]
    public void Onboarding_is_skippable_at_any_step(OnboardingStep step)
    {
        var h = NewVm();
        h.Vm.CurrentStep = step;

        h.Vm.SkipCommand.Execute(null);

        Assert.True(h.State.IsCompleted);
        Assert.Equal(typeof(ProjectsHomeViewModel), h.Navigation.LastNavigatedTo);
    }
}

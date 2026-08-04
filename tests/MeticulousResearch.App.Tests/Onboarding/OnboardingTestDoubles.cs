using System.ComponentModel;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests.Onboarding;

/// <summary>
/// Shared window-free doubles for the onboarding <c>@unit</c> tests: an in-memory onboarding state,
/// a recording navigation service, a configurable key tester, a recording sample-project factory,
/// and an in-memory settings service. Keep onboarding logic assertions container- and window-free
/// (TESTING-STRATEGY §2).
/// </summary>
internal sealed class FakeOnboardingState : IOnboardingState
{
    public bool IsCompleted { get; private set; }
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Welcome;
    public int MarkCompletedCount { get; private set; }
    public int ResetCount { get; private set; }

    public void MarkCompleted()
    {
        MarkCompletedCount++;
        IsCompleted = true;
    }

    public void Reset()
    {
        ResetCount++;
        IsCompleted = false;
        CurrentStep = OnboardingStep.Welcome;
    }
}

internal sealed class RecordingNavigationService : INavigationService
{
    public Type? LastNavigatedTo { get; private set; }
    public int NavigateCount { get; private set; }

    public ViewModelBase? CurrentViewModel => null;
    public string? ActiveProjectId => null;
    public bool CanGoBack => false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TViewModel NavigateTo<TViewModel>(params object[] parameters) where TViewModel : ViewModelBase
    {
        LastNavigatedTo = typeof(TViewModel);
        NavigateCount++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentViewModel)));
        return null!;
    }

    public void Back()
    {
    }
}

internal sealed class ConfigurableKeyTester : IKeyTester
{
    private KeyTestResult _result = KeyTestResult.Ok(Array.Empty<string>());
    public int TestCount { get; private set; }

    public ConfigurableKeyTester Returns(KeyTestResult result)
    {
        _result = result;
        return this;
    }

    public Task<KeyTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        TestCount++;
        return Task.FromResult(_result);
    }
}

internal sealed class RecordingSampleProjectFactory : ISampleProjectFactory
{
    private int _seq;
    public int CreateCount { get; private set; }

    public Core.Data.Entities.Project CreateSampleProject()
    {
        CreateCount++;
        return new Core.Data.Entities.Project { Id = $"sample-{++_seq}", Name = SampleContent.ProjectName };
    }
}

internal sealed class StubDirectoryValidator : IDataDirectoryValidator
{
    private readonly bool _writable;
    public StubDirectoryValidator(bool writable) => _writable = writable;
    public bool IsWritable(string path) => _writable;
}

internal sealed class OnboardingInMemorySettings : ISettingsService
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

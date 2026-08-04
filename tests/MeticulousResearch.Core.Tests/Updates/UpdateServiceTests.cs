using MeticulousResearch.Core.AppInfo;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Updates;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.Core.Tests.Updates;

/// <summary>
/// @unit tests for update-notice (docs/features/update-notice/tests.md, SPEC §8): semantic version
/// comparison, malformed-input safety, notice state + dismissal memory, non-blocking behavior,
/// silent failure, and up-to-date. All run window- and network-free via a fake latest-version
/// provider (TESTING-STRATEGY §4).
/// </summary>
public sealed class UpdateServiceTests
{
    private sealed class FakeAppInfo : IAppInfo
    {
        public string ProductName => "MeticulousResearch Desktop";
        public string Version { get; set; } = "1.0.0";
        public string IconResource => "AppIcon";
    }

    private sealed class StubSettings : ISettingsService
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

    private sealed class FakeLatestVersionProvider : ILatestVersionProvider
    {
        private readonly Func<CancellationToken, Task<string?>> _get;
        public FakeLatestVersionProvider(string? latest) => _get = _ => Task.FromResult(latest);
        public FakeLatestVersionProvider(Func<CancellationToken, Task<string?>> get) => _get = get;
        public Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default) => _get(cancellationToken);
    }

    private sealed class RecordingErrorLog : IErrorLog
    {
        public int Count { get; private set; }
        public void LogUnexpected(string context, Exception exception) => Count++;
    }

    private static UpdateService Create(
        string current,
        ILatestVersionProvider provider,
        ISettingsService? settings = null,
        IErrorLog? errorLog = null) =>
        new(new FakeAppInfo { Version = current }, provider, settings ?? new StubSettings(), errorLog);

    // @unit
    // Scenario Outline: Comparing the current version to the latest available
    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.1", "1.0.1", false)]
    [InlineData("1.2.0", "1.1.9", false)]
    public async Task Comparing_the_current_version_to_the_latest_available(string current, string latest, bool available)
    {
        var svc = Create(current, new FakeLatestVersionProvider(latest));

        var result = await svc.CheckForUpdatesAsync();

        Assert.Equal(available, result.IsUpdateAvailable);
    }

    // @unit
    // Scenario: Pre-release or malformed version strings do not trigger a false notice
    [Theory]
    [InlineData("not-a-version")]
    [InlineData("1.0.0-beta")]
    [InlineData("")]
    [InlineData("banana")]
    public async Task Malformed_latest_version_does_not_trigger_a_false_notice(string malformed)
    {
        var log = new RecordingErrorLog();
        var svc = Create("1.0.0", new FakeLatestVersionProvider(malformed), errorLog: log);

        var result = await svc.CheckForUpdatesAsync();

        Assert.False(result.IsUpdateAvailable); // no update considered available
        Assert.Equal(0, log.Count);             // malformed input is not an error surfaced to the user
    }

    // @unit
    // Scenario: An available update produces a dismissible notice state
    [Fact]
    public async Task An_available_update_produces_a_dismissible_notice_state()
    {
        var svc = Create("1.0.0", new FakeLatestVersionProvider("1.0.1"));

        var result = await svc.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);   // an "update available" notice state is raised
        Assert.Equal("1.0.1", result.NewVersion); // it includes the new version number
        Assert.False(result.IsBlocking);          // marked non-blocking
        Assert.True(result.IsDismissible);        // and dismissible
    }

    // @unit
    // Scenario: A dismissed notice is not shown again for the same version
    [Fact]
    public async Task A_dismissed_notice_is_not_shown_again_for_the_same_version()
    {
        var settings = new StubSettings();
        var svc = Create("1.0.0", new FakeLatestVersionProvider("1.0.1"), settings);

        // Given I dismissed the notice for version "1.0.1".
        svc.Dismiss("1.0.1");
        Assert.Equal("1.0.1", settings.DismissedUpdateVersion);

        // When the app checks again and the latest is still "1.0.1" -> the notice is not raised again.
        var again = await svc.CheckForUpdatesAsync();
        Assert.False(again.IsUpdateAvailable);

        // And it will be raised again if a newer version than "1.0.1" appears.
        var newer = Create("1.0.0", new FakeLatestVersionProvider("1.0.2"), settings);
        var raised = await newer.CheckForUpdatesAsync();
        Assert.True(raised.IsUpdateAvailable);
        Assert.Equal("1.0.2", raised.NewVersion);
    }

    // @unit
    // Scenario: The update check never blocks app usage
    [Fact]
    public async Task The_update_check_never_blocks_app_usage()
    {
        var gate = new TaskCompletionSource<string?>();
        var svc = Create("1.0.0", new FakeLatestVersionProvider(_ => gate.Task));

        // Given the update check is slow or pending: the call returns a Task that is not yet
        // completed, so nothing is blocked waiting on it.
        var checkTask = svc.CheckForUpdatesAsync();
        Assert.False(checkTask.IsCompleted);

        // The app can keep working; once the check eventually resolves it does so without error.
        gate.SetResult("1.0.1");
        var result = await checkTask;
        Assert.True(result.IsUpdateAvailable);
    }

    // @unit
    // Scenario: An update check failure is silent to the user
    [Fact]
    public async Task An_update_check_failure_is_silent_to_the_user()
    {
        var log = new RecordingErrorLog();
        var provider = new FakeLatestVersionProvider(_ => throw new HttpRequestExceptionStub());
        var svc = Create("1.0.0", provider, errorLog: log);

        var result = await svc.CheckForUpdatesAsync(); // does not throw

        Assert.False(result.IsUpdateAvailable); // no update notice is shown
        Assert.True(log.Count > 0);             // detail is logged off-screen only, never surfaced raw
    }

    private sealed class HttpRequestExceptionStub : Exception { }

    // @unit
    // Scenario: Being up to date shows no notice
    [Fact]
    public async Task Being_up_to_date_shows_no_notice()
    {
        var svc = Create("1.0.1", new FakeLatestVersionProvider("1.0.1"));

        var result = await svc.CheckForUpdatesAsync();

        Assert.False(result.IsUpdateAvailable);
    }
}

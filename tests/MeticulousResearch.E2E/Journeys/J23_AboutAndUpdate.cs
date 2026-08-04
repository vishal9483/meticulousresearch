using MeticulousResearch.Core.AppInfo;
using MeticulousResearch.Core.Updates;
using MeticulousResearch.Core.ViewStates;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-23 — About screen &amp; update notice (covers SPEC §3.7, §8). The About screen's product identity
/// and version, and the update service's availability decision, run headlessly over the real
/// <see cref="AssemblyAppInfo"/> and <see cref="UpdateService"/> (the window rendering is a ui journey).
/// </summary>
public sealed class J23_AboutAndUpdate : IDisposable
{
    private readonly JourneyHarness _h = new();

    public void Dispose() => _h.Dispose();

    private sealed class StubAppInfo : IAppInfo
    {
        public StubAppInfo(string version) => Version = version;
        public string ProductName => "MeticulousResearch Desktop";
        public string Version { get; }
        public string IconResource => "AppIcon";
    }

    private sealed class StubLatestVersionProvider : ILatestVersionProvider
    {
        private readonly string? _latest;
        public StubLatestVersionProvider(string? latest) => _latest = latest;
        public Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_latest);
    }

    private sealed class NullErrorLog : IErrorLog
    {
        public void LogUnexpected(string context, Exception exception) { }
    }

    // @e2e @unit
    // Scenario: The About screen shows product identity and version
    [Fact]
    public void The_about_screen_shows_product_identity_and_version()
    {
        var appInfo = new AssemblyAppInfo(typeof(JourneyHarness).Assembly);
        Assert.False(string.IsNullOrWhiteSpace(appInfo.ProductName));
        Assert.False(string.IsNullOrWhiteSpace(appInfo.Version));
    }

    // @e2e @unit
    // Scenario Outline: The update service reports availability correctly
    [Theory]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    public async Task The_update_service_reports_availability_correctly(string current, string latest, bool shown)
    {
        var service = new UpdateService(
            new StubAppInfo(current),
            new StubLatestVersionProvider(latest),
            _h.Settings,
            new NullErrorLog());

        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(shown, result.IsUpdateAvailable);
    }
}

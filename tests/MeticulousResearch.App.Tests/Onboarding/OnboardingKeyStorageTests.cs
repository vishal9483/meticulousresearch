using System.Text;
using MeticulousResearch.App.Services;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Onboarding;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Time;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests.Onboarding;

/// <summary>
/// Faithful <c>@unit</c> translation of the "stored securely, not in plaintext" onboarding scenario
/// (docs/features/onboarding/tests.md, SPEC §3.8(2), §7.5). Completes the key step over a real
/// <see cref="DataStore"/>/<see cref="SettingsService"/> and asserts the key is retrievable from the
/// secure store while never appearing in the on-disk <c>db.sqlite</c> (the settings live in that db).
/// </summary>
public sealed class OnboardingKeyStorageTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;

    public OnboardingKeyStorageTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-onboarding-key", Guid.NewGuid().ToString("N"));
        _store = new DataStore(new FixedClock(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero)), _dataDir);
        _store.Initialize();
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // Scenario: The key entered in onboarding is stored securely, not in plaintext
    //   Given I completed the API key step with a valid key
    //   Then the key is retrievable via the secure key store
    //   And the key string does not appear in db.sqlite or any settings file
    [Fact]
    public async Task Key_entered_in_onboarding_is_stored_securely_not_in_plaintext()
    {
        const string key = "sk-onboarding-secret-9f3a2b";

        var keyStore = new FakeSecureKeyStore();
        var settings = new SettingsService(_store);
        var env = new FakeEnvironment();
        var credentials = new ApiCredentialProvider(env, keyStore, settings);
        var keyTester = new ConfigurableKeyTester().Returns(KeyTestResult.Ok(new[] { "claude-opus-5" }));

        var vm = new OnboardingViewModel(
            new FakeOnboardingState(), keyStore, credentials, keyTester, settings,
            new StubDirectoryValidator(writable: true),
            new RecordingSampleProjectFactory(), new RecordingNavigationService(), new FirstRunHints(),
            _dataDir);

        // Given I completed the API key step with a valid key
        vm.CurrentStep = OnboardingStep.ApiKey;
        vm.ApiKeyInput = key;
        await vm.TestKeyAsync();
        Assert.True(vm.TryAdvance());

        // and persisted the defaults (so the settings db is written) before checking it.
        Assert.True(vm.TryAdvance());

        // the key is retrievable via the secure key store
        Assert.Equal(key, keyStore.Get());

        // the key string does not appear in db.sqlite or the WAL (settings live in that db; there
        // is no separate plaintext settings file).
        foreach (var path in DatabaseFiles())
        {
            var bytes = ReadShared(path);
            Assert.DoesNotContain(key, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain(key, Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
        }
    }

    private IEnumerable<string> DatabaseFiles()
    {
        yield return _store.DatabasePath;
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = _store.DatabasePath + suffix;
            if (File.Exists(path))
                yield return path;
        }
    }

    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private sealed class FixedClock : IClock
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public DateTimeOffset UtcNow => _now;
    }
}

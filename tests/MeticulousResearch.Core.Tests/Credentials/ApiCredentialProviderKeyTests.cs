using MeticulousResearch.Core.Credentials;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Credentials;

/// <summary>
/// @unit / @integration scenarios for API-key resolution (env wins) and the guarantee that an
/// env-supplied key is never persisted (docs/features/settings-secure-key/tests.md).
/// </summary>
public sealed class ApiCredentialProviderKeyTests
{
    private static ApiCredentialProvider NewProvider(
        FakeEnvironment env, FakeSecureKeyStore keyStore, ISettingsService settings)
        => new(env, keyStore, settings);

    // @unit
    // Scenario: The ANTHROPIC_API_KEY environment variable takes precedence over the stored key
    [Fact]
    public void Env_key_takes_precedence_over_stored_key()
    {
        var keyStore = new FakeSecureKeyStore();
        keyStore.Save("sk-stored");
        var env = new FakeEnvironment().Set("ANTHROPIC_API_KEY", "sk-from-env");
        var provider = NewProvider(env, keyStore, new StubSettings());

        Assert.Equal("sk-from-env", provider.ResolveApiKey());
    }

    // @unit
    // Scenario: The stored key is used when no environment variable is set
    [Fact]
    public void Stored_key_used_when_no_env_variable()
    {
        var keyStore = new FakeSecureKeyStore();
        keyStore.Save("sk-stored");
        var env = new FakeEnvironment(); // ANTHROPIC_API_KEY not set
        var provider = NewProvider(env, keyStore, new StubSettings());

        Assert.Equal("sk-stored", provider.ResolveApiKey());
    }

    // @unit
    // Scenario: An empty environment variable does not override the stored key
    [Fact]
    public void Empty_env_variable_does_not_override_stored_key()
    {
        var keyStore = new FakeSecureKeyStore();
        keyStore.Save("sk-stored");
        var env = new FakeEnvironment().Set("ANTHROPIC_API_KEY", "");
        var provider = NewProvider(env, keyStore, new StubSettings());

        Assert.Equal("sk-stored", provider.ResolveApiKey());
    }

    // @unit
    // Scenario: No key anywhere reports that no key is configured
    [Fact]
    public void No_key_anywhere_reports_no_key_configured()
    {
        var keyStore = new FakeSecureKeyStore(); // nothing saved
        var env = new FakeEnvironment();          // not set
        var provider = NewProvider(env, keyStore, new StubSettings());

        Assert.Null(provider.ResolveApiKey());
        Assert.False(provider.HasApiKey);
    }

    // @unit @integration
    // Scenario: A key supplied via the environment is never written to storage
    [Fact]
    public void Env_supplied_key_is_never_written_to_storage()
    {
        using var temp = new TempDataDirectory();
        var store = new DataStore(new FakeClock(), temp.Path);
        store.Initialize();
        var settings = new SettingsService(store);
        var keyStore = new FakeSecureKeyStore(); // no key saved
        var env = new FakeEnvironment().Set("ANTHROPIC_API_KEY", "sk-from-env");
        var provider = NewProvider(env, keyStore, settings);

        var resolved = provider.ResolveApiKey();

        Assert.Equal("sk-from-env", resolved);
        // never written to db.sqlite
        Assert.False(FileContainsText(store.DatabasePath, "sk-from-env"));
        // never written to any settings file on disk
        Assert.False(AnyFileContainsText(temp.Path, "sk-from-env"));
        // the secure key store still reports no key configured
        Assert.False(keyStore.HasKey);
        Assert.Null(keyStore.Get());
    }

    private static bool FileContainsText(string path, string text)
    {
        if (!File.Exists(path)) return false;
        var bytes = ReadAllBytesShared(path);
        var needle = System.Text.Encoding.UTF8.GetBytes(text);
        for (int i = 0; i <= bytes.Length - needle.Length && needle.Length > 0; i++)
        {
            var match = true;
            for (int j = 0; j < needle.Length; j++)
                if (bytes[i + j] != needle[j]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    private static bool AnyFileContainsText(string root, string text)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            if (FileContainsText(file, text)) return true;
        return false;
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }
}

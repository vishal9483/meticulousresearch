using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Security;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Security;

/// <summary>
/// @unit / @integration scenarios for secure API-key storage
/// (docs/features/settings-secure-key/tests.md — "Secure key storage").
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SecureKeyStoreTests
{
    // @unit @integration
    // Scenario: Saving an API key does not persist it in the database or plaintext
    [Fact]
    public void Saving_a_key_does_not_persist_it_in_the_database_or_plaintext()
    {
        using var temp = new TempDataDirectory();
        var store = new DataStore(new FakeClock(), temp.Path);
        store.Initialize();
        var keyStore = new DpapiSecureKeyStore(Path.Combine(temp.Path, "credentials.dat"));

        keyStore.Save("sk-ant-secret");

        // the key is retrievable via the secure key store
        Assert.Equal("sk-ant-secret", keyStore.Get());

        // "sk-ant-secret" does not appear in db.sqlite
        Assert.False(FileContainsText(store.DatabasePath, "sk-ant-secret"),
            "The API key must never be written to db.sqlite.");

        // "sk-ant-secret" does not appear in any settings file on disk
        Assert.False(AnyFileContainsText(temp.Path, "sk-ant-secret"),
            "The API key must never be written in plaintext to any file on disk.");
    }

    // @unit
    // Scenario: Retrieving the key when none is set returns empty state
    [Fact]
    public void Retrieving_the_key_when_none_is_set_reports_no_key()
    {
        var keyStore = new FakeSecureKeyStore();

        Assert.False(keyStore.HasKey);
        Assert.Null(keyStore.Get());
    }

    // @unit @integration
    // Scenario: Overwriting the API key replaces the stored value
    [Fact]
    public void Overwriting_the_key_replaces_the_stored_value()
    {
        using var temp = new TempDataDirectory();
        var keyStore = new DpapiSecureKeyStore(Path.Combine(temp.Path, "credentials.dat"));
        keyStore.Save("sk-old");

        keyStore.Save("sk-new");

        Assert.Equal("sk-new", keyStore.Get());
    }

    // @unit @integration
    // Scenario: Clearing the API key removes it from secure storage
    [Fact]
    public void Clearing_the_key_removes_it_from_secure_storage()
    {
        using var temp = new TempDataDirectory();
        var keyStore = new DpapiSecureKeyStore(Path.Combine(temp.Path, "credentials.dat"));
        keyStore.Save("sk-ant-secret");

        keyStore.Clear();

        Assert.False(keyStore.HasKey);
        Assert.Null(keyStore.Get());
    }

    private static bool FileContainsText(string path, string text)
    {
        if (!File.Exists(path)) return false;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        var bytes = ms.ToArray();
        var needle = System.Text.Encoding.UTF8.GetBytes(text);
        return IndexOf(bytes, needle) >= 0;
    }

    private static bool AnyFileContainsText(string root, string text)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (FileContainsText(file, text))
                return true;
        }
        return false;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}

using MeticulousResearch.Core.Security;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// In-memory <see cref="ISecureKeyStore"/> for tests. Holds the key in a private field so
/// credential-resolution and settings tests never touch DPAPI or the filesystem.
/// </summary>
public sealed class FakeSecureKeyStore : ISecureKeyStore
{
    private string? _key;

    public bool HasKey => !string.IsNullOrEmpty(_key);

    public void Save(string key) => _key = key ?? throw new ArgumentNullException(nameof(key));

    public string? Get() => string.IsNullOrEmpty(_key) ? null : _key;

    public void Clear() => _key = null;
}

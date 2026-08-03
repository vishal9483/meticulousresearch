using System.Security.Cryptography;
using System.Text;

namespace MeticulousResearch.Core.Security;

/// <summary>
/// <see cref="ISecureKeyStore"/> backed by Windows DPAPI (<see cref="ProtectedData"/>). The key is
/// encrypted for the current user and written as an opaque blob; the plaintext never touches disk,
/// SQLite, or any settings file (settings-secure-key/phase.md, SPEC §7.5).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DpapiSecureKeyStore : ISecureKeyStore
{
    private readonly string _blobPath;

    /// <summary>Creates a store that persists the encrypted key blob at <paramref name="blobPath"/>.</summary>
    public DpapiSecureKeyStore(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            throw new ArgumentException("Blob path must be a non-empty path.", nameof(blobPath));
        _blobPath = System.IO.Path.GetFullPath(blobPath);
    }

    /// <inheritdoc />
    public bool HasKey => File.Exists(_blobPath) && new FileInfo(_blobPath).Length > 0;

    /// <inheritdoc />
    public void Save(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        var dir = System.IO.Path.GetDirectoryName(_blobPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var plaintext = Encoding.UTF8.GetBytes(key);
        var encrypted = ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_blobPath, encrypted);
    }

    /// <inheritdoc />
    public string? Get()
    {
        if (!HasKey) return null;
        var encrypted = File.ReadAllBytes(_blobPath);
        var plaintext = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintext);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (File.Exists(_blobPath))
            File.Delete(_blobPath);
    }
}

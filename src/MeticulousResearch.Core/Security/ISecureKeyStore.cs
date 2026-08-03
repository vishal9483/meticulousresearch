namespace MeticulousResearch.Core.Security;

/// <summary>
/// Secure storage for the Anthropic API key (settings-secure-key/phase.md, SPEC §7.5). The value
/// is held by the operating-system credential vault (Windows Credential Manager / DPAPI) and is
/// never written to SQLite or any plaintext settings file. Consumed only via
/// <c>IApiCredentialProvider</c> for resolution; nothing else reads the store directly.
/// </summary>
public interface ISecureKeyStore
{
    /// <summary>True when a key is currently stored.</summary>
    bool HasKey { get; }

    /// <summary>Stores (or overwrites) the API key. The plaintext is never persisted unencrypted.</summary>
    void Save(string key);

    /// <summary>Returns the stored key, or <c>null</c> when none is configured.</summary>
    string? Get();

    /// <summary>Removes the stored key from secure storage. A no-op when nothing is stored.</summary>
    void Clear();
}

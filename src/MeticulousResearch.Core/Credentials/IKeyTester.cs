namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// Validates the effective API key by calling the Models endpoint at the <b>resolved</b> base URL
/// (settings-secure-key/phase.md, SPEC §3.8(2)). Maps 401 → invalid key and network failures →
/// an offline message; never surfaces a raw stack trace.
/// </summary>
public interface IKeyTester
{
    /// <summary>Tests the resolved key against the resolved base URL and returns the outcome.</summary>
    Task<KeyTestResult> TestAsync(CancellationToken cancellationToken = default);
}

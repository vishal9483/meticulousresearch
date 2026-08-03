namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// The outcome of a "Test key" check (settings-secure-key/phase.md). On success it carries the
/// model list returned by the API; on failure it carries a human-readable, actionable message
/// with no stack trace.
/// </summary>
public sealed class KeyTestResult
{
    private KeyTestResult(bool success, IReadOnlyList<string> models, string? errorMessage)
    {
        Success = success;
        Models = models;
        ErrorMessage = errorMessage;
    }

    /// <summary>True when the key validated successfully.</summary>
    public bool Success { get; }

    /// <summary>The models returned by the API (empty on failure).</summary>
    public IReadOnlyList<string> Models { get; }

    /// <summary>A human-readable error message on failure, otherwise <c>null</c>.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful result with the returned model list.</summary>
    public static KeyTestResult Ok(IReadOnlyList<string> models) => new(true, models, null);

    /// <summary>Creates a failed result with an actionable message.</summary>
    public static KeyTestResult Failure(string message) => new(false, Array.Empty<string>(), message);
}

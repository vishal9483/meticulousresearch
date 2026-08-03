namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// An exception that already carries a <see cref="UserFacingFailureKind"/> classification, so an
/// operation can fail with a known, mappable failure (missing key, offline, rate limited, extraction
/// failed) rather than an opaque exception. Anything that is <em>not</em> a
/// <see cref="UserFacingException"/> is treated as unexpected — a generic message with the detail
/// logged (SPEC §3.7).
/// </summary>
public sealed class UserFacingException : Exception
{
    /// <summary>Creates a classified user-facing failure.</summary>
    /// <param name="kind">The classified failure kind.</param>
    /// <param name="message">Optional developer/log message; never shown to the user.</param>
    /// <param name="inner">The underlying exception, if any.</param>
    public UserFacingException(UserFacingFailureKind kind, string? message = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    /// <summary>The classified failure kind used to build the user-facing message + recovery.</summary>
    public UserFacingFailureKind Kind { get; }
}

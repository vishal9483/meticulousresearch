namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Thrown when the sidecar exits mid-stream. The gateway converts it into a <b>retryable</b>
/// <see cref="ChatFaulted"/> so the turn is not silently lost (SPEC §8).
/// </summary>
public sealed class SidecarCrashedException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public SidecarCrashedException()
        : base("The sidecar process exited mid-stream.")
    {
    }

    /// <summary>Creates the exception with a specific message.</summary>
    public SidecarCrashedException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when the sidecar cannot be (re)started — most notably after repeated immediate crashes are
/// throttled (SPEC §8). The gateway converts it into a non-retryable "backend unavailable"
/// <see cref="ChatFaulted"/> with a recovery hint.
/// </summary>
public sealed class SidecarUnavailableException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public SidecarUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the underlying cause.</summary>
    public SidecarUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

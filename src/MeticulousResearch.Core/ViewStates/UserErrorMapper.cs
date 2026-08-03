namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// Default <see cref="IUserErrorMapper"/>: a pure mapping from a failure classification to a
/// human-readable message + recovery action (SPEC §3.7). Unexpected exceptions are logged via
/// <see cref="IErrorLog"/> and mapped to a generic message so a raw stack trace never reaches the UI.
/// </summary>
public sealed class UserErrorMapper : IUserErrorMapper
{
    /// <summary>The generic message shown for any unexpected/unclassified failure.</summary>
    public const string GenericMessage = "Something went wrong. Please try again.";

    private readonly IErrorLog? _log;

    /// <summary>Creates the mapper, optionally logging unexpected-exception detail off-screen.</summary>
    /// <param name="log">Sink for raw exception detail; when null, detail is simply not surfaced.</param>
    public UserErrorMapper(IErrorLog? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public UserError Map(UserFacingFailureKind kind) => kind switch
    {
        UserFacingFailureKind.MissingApiKey =>
            new UserError("No API key configured", "Open Settings"),
        UserFacingFailureKind.Offline =>
            new UserError("You appear to be offline", "Retry"),
        UserFacingFailureKind.RateLimited =>
            new UserError("Rate limited — the app is retrying", "Retry"),
        UserFacingFailureKind.ExtractionFailed =>
            new UserError("Could not read this file", "Re-extract"),
        _ => new UserError(GenericMessage, "Retry"),
    };

    /// <inheritdoc />
    public UserError FromException(Exception exception, string context = "operation")
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        if (exception is UserFacingException classified)
            return Map(classified.Kind);

        // Unexpected: log the raw detail off-screen and show only a generic message.
        _log?.LogUnexpected(context, exception);
        return new UserError(GenericMessage, "Retry");
    }
}

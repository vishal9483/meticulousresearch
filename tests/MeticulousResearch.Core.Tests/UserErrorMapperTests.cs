using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.Core.Tests;

/// <summary>
/// @unit tests for the shared user-facing error mapper (docs/features/empty-loading-error-states/
/// tests.md — Error states). Each known failure must map to a specific human-readable message and
/// recovery action, and an unexpected exception must map to a generic message with the raw detail
/// logged off-screen — never a stack trace on the screen (SPEC §3.7).
/// </summary>
public class UserErrorMapperTests
{
    private sealed class RecordingErrorLog : IErrorLog
    {
        public List<(string Context, Exception Exception)> Entries { get; } = new();

        public void LogUnexpected(string context, Exception exception) =>
            Entries.Add((context, exception));
    }

    // Scenario Outline: Known failures map to a human-readable, actionable error state
    //   Given an operation fails with "<failure>"
    //   When the view handles the failure
    //   Then it shows the message "<message>"
    //   And it offers the recovery action "<recovery>"
    //   And no raw stack trace is shown
    [Theory]
    [InlineData(UserFacingFailureKind.MissingApiKey, "No API key configured", "Open Settings")]
    [InlineData(UserFacingFailureKind.Offline, "You appear to be offline", "Retry")]
    [InlineData(UserFacingFailureKind.RateLimited, "Rate limited — the app is retrying", "Retry")]
    [InlineData(UserFacingFailureKind.ExtractionFailed, "Could not read this file", "Re-extract")]
    public void Known_failures_map_to_message_and_recovery(
        UserFacingFailureKind failure, string expectedMessage, string expectedRecovery)
    {
        var mapper = new UserErrorMapper();

        var error = mapper.Map(failure);

        Assert.Equal(expectedMessage, error.Message);
        Assert.Equal(expectedRecovery, error.RecoveryAction);
        // No raw stack trace is shown: the message is exactly the designed text.
        Assert.DoesNotContain("Exception", error.Message);
        Assert.DoesNotContain("   at ", error.Message);
    }

    // Scenario Outline routed through an exception classification as well: a classified
    // UserFacingException maps by its kind (the same table above).
    [Theory]
    [InlineData(UserFacingFailureKind.MissingApiKey, "No API key configured", "Open Settings")]
    [InlineData(UserFacingFailureKind.Offline, "You appear to be offline", "Retry")]
    [InlineData(UserFacingFailureKind.RateLimited, "Rate limited — the app is retrying", "Retry")]
    [InlineData(UserFacingFailureKind.ExtractionFailed, "Could not read this file", "Re-extract")]
    public void Classified_exceptions_map_to_message_and_recovery(
        UserFacingFailureKind failure, string expectedMessage, string expectedRecovery)
    {
        var log = new RecordingErrorLog();
        var mapper = new UserErrorMapper(log);

        var error = mapper.FromException(new UserFacingException(failure, "internal detail"));

        Assert.Equal(expectedMessage, error.Message);
        Assert.Equal(expectedRecovery, error.RecoveryAction);
        // A classified, known failure is not treated as unexpected, so nothing is logged.
        Assert.Empty(log.Entries);
    }

    // Scenario: Error states never surface a raw exception message
    //   Given an operation throws an unexpected exception
    //   When the view handles the failure
    //   Then it shows a generic human-readable error
    //   And the exception detail is written to the log, not the screen
    [Fact]
    public void Unexpected_exception_shows_generic_message_and_logs_detail()
    {
        var log = new RecordingErrorLog();
        var mapper = new UserErrorMapper(log);
        var raw = new InvalidOperationException("SECRET-STACK-DETAIL at Internals.Boom()");

        var error = mapper.FromException(raw, context: "ResourcesView");

        // Generic human-readable error — not the raw exception text.
        Assert.Equal(UserErrorMapper.GenericMessage, error.Message);
        Assert.DoesNotContain("SECRET-STACK-DETAIL", error.Message);
        Assert.DoesNotContain("Internals.Boom", error.Message);
        Assert.NotEqual(raw.Message, error.Message);

        // The exception detail is written to the log, not the screen.
        var entry = Assert.Single(log.Entries);
        Assert.Equal("ResourcesView", entry.Context);
        Assert.Same(raw, entry.Exception);
    }
}

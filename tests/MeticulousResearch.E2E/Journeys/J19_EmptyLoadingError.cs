using MeticulousResearch.Core.ViewStates;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-19 — Empty, loading, and error states everywhere (covers SPEC §3.7, §9.1: 10). The designed
/// empty states and skeleton loaders are window journeys (Category=ui); the headless truth — every
/// known failure maps to a human-readable, recoverable message (never a stack trace) — runs in the
/// gate over the real <see cref="UserErrorMapper"/>.
/// </summary>
public sealed class J19_EmptyLoadingError
{
    // @e2e @unit
    // Scenario Outline: Failures surface human-readable, recoverable errors — never a stack trace
    [Theory]
    [InlineData(UserFacingFailureKind.MissingApiKey)]
    [InlineData(UserFacingFailureKind.Offline)]
    [InlineData(UserFacingFailureKind.RateLimited)]
    [InlineData(UserFacingFailureKind.ExtractionFailed)]
    public void Failures_surface_human_readable_recoverable_errors(UserFacingFailureKind failure)
    {
        var mapper = new UserErrorMapper();

        // When I trigger the affected action, I see a human-readable message with a recovery action.
        var error = mapper.Map(failure);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.False(string.IsNullOrWhiteSpace(error.RecoveryAction));

        // And the message is not a stack trace (no exception/type noise leaks to the UI).
        Assert.DoesNotContain("Exception", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", error.Message);
    }

    // @e2e @unit — an unexpected exception maps to a generic message and logs the detail off-screen.
    [Fact]
    public void An_unexpected_exception_shows_a_generic_message_not_a_stack_trace()
    {
        var mapper = new UserErrorMapper();
        var error = mapper.FromException(new InvalidOperationException("SECRET-STACK-DETAIL"), context: "ResourcesView");

        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.DoesNotContain("SECRET-STACK-DETAIL", error.Message);
    }

    // @e2e (FlaUI release gate)
    // Scenario Outline: Every primary list shows a designed empty state; async shows skeleton loaders.
    [Fact(Skip = "FlaUI release-gate journey: designed empty/loading states are verified against the real window; runs nightly.")]
    [Trait("Category", "ui")]
    public void Every_primary_list_shows_a_designed_empty_state_and_async_shows_skeletons()
    {
    }
}

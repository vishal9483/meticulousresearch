using MeticulousResearch.Core.Ai;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Sidecar auto-restart scenarios (SPEC §8): a crashed sidecar restarts with a fresh session, an
/// in-flight crash surfaces a retryable error preserving partial work, and repeated immediate crashes
/// back off and report an unavailable backend.
/// </summary>
public sealed class SidecarAutoRestartTests
{
    // @unit @integration
    // Scenario: A crashed sidecar is automatically restarted
    [Fact]
    [Trait("integration", "true")]
    public void Crashed_sidecar_is_restarted_with_new_token_and_endpoint()
    {
        var clock = new FakeClock();
        var factory = new FakeSidecarProcessFactory();
        var supervisor = new SidecarSupervisor(factory, clock);
        var startInfo = new SidecarStartInfo("https://api.anthropic.com");

        var first = supervisor.EnsureRunning(startInfo);

        // The sidecar runs for a while, then exits unexpectedly.
        clock.Advance(TimeSpan.FromSeconds(30));
        ((FakeSidecarProcess)first).SimulateExit();

        var second = supervisor.EnsureRunning(startInfo);

        Assert.NotSame(first, second);
        Assert.True(second.HasExited == false);
        // A new per-session token and endpoint are established.
        Assert.NotEqual(first.Endpoint.Token, second.Endpoint.Token);
        Assert.NotEqual(first.Endpoint.Port, second.Endpoint.Port);
    }

    // @unit
    // Scenario: An in-flight request during a sidecar crash surfaces a retryable error, not a lost turn
    [Fact]
    public async Task In_flight_crash_surfaces_retryable_error_and_preserves_partial()
    {
        var factory = new FakeSidecarProcessFactory
        {
            Configure = p =>
            {
                p.WithTokens("Partial answer");
                p.CrashAtEnd = true; // crash after delivering the partial token
            },
        };
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        var service = new SidecarChatService(
            AiTestHelpers.Credentials(storedKey: "sk-stored"), new ChatRequestAssembler(), supervisor);

        var events = await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));

        // Partial work is preserved for the caller.
        var deltas = events.OfType<ChatTokenDelta>().Select(d => d.Text).ToArray();
        Assert.Equal(new[] { "Partial answer" }, deltas);

        // The turn fails with a retryable error — not a completion.
        var fault = Assert.IsType<ChatFaulted>(events[^1]);
        Assert.True(fault.Retryable);
        Assert.DoesNotContain(events, e => e is ChatCompleted);
    }

    // @unit
    // Scenario: Repeated immediate crashes back off and report an unavailable backend
    [Fact]
    public async Task Repeated_immediate_crashes_throttle_and_report_unavailable()
    {
        var factory = new FakeSidecarProcessFactory { CrashOnLaunch = true };
        var supervisor = new SidecarSupervisor(factory, new FakeClock());
        var service = new SidecarChatService(
            AiTestHelpers.Credentials(storedKey: "sk-stored"), new ChatRequestAssembler(), supervisor);

        ChatFaulted? last = null;
        var sawUnavailable = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var events = await AiTestHelpers.Collect(service.Ask(AiTestHelpers.Context()));
            last = Assert.IsType<ChatFaulted>(events[^1]);
            if (last.Kind == ChatErrorKind.BackendUnavailable)
                sawUnavailable = true;
        }

        // Restart attempts are throttled: the factory is not launched once per attempt forever.
        Assert.True(factory.StartCount < 5, "launches should be throttled after repeated crashes");

        // The user sees a clear "backend unavailable" error with a recovery hint.
        Assert.True(sawUnavailable);
        Assert.Equal(ChatErrorKind.BackendUnavailable, last!.Kind);
        Assert.False(last.Retryable);
        Assert.Contains("unavailable", last.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("try again", last.Message, StringComparison.OrdinalIgnoreCase);
    }
}

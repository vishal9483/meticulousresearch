using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Tests.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Backend-selection scenarios (SPEC §7.2): the sidecar is the default, settings choose the active
/// backend, and the rest of the app cannot tell which backend answered.
/// </summary>
public sealed class BackendSelectionTests
{
    // @unit
    // Scenario: The sidecar is the default backend
    [Fact]
    public void Sidecar_is_the_default_backend()
    {
        var settings = new StubSettings(); // fresh install: no stored preference
        var sidecar = new FakeChatService();
        var direct = new FakeChatService();
        var factory = new ChatBackendFactory(settings, () => sidecar, () => direct);

        Assert.Equal(ChatBackendKind.Sidecar, factory.Active);
        Assert.Same(sidecar, factory.Resolve());
    }

    // @unit
    // Scenario Outline: Settings selects which backend is active
    [Theory]
    [InlineData("sidecar", ChatBackendKind.Sidecar)]
    [InlineData("direct-api", ChatBackendKind.DirectApi)]
    public void Settings_selects_active_backend(string choice, ChatBackendKind active)
    {
        var settings = new StubSettings { ChatBackend = choice };
        var sidecar = new FakeChatService();
        var direct = new FakeChatService();
        var factory = new ChatBackendFactory(settings, () => sidecar, () => direct);

        Assert.Equal(active, factory.Active);
        Assert.Same(active == ChatBackendKind.DirectApi ? direct : sidecar, factory.Resolve());
    }

    // @unit
    // Scenario: The rest of the app cannot tell which backend answered
    [Fact]
    public async Task Backends_are_equivalent_for_identical_scripted_output()
    {
        var usage = new ChatUsage(50, 12, 4, 2);
        var tokens = new[] { "Answer", " here" };

        var sidecarEvents = await AiTestHelpers.Collect(
            BackendFixtures.Build(BackendFixtures.Sidecar, usage, tokens).Ask(AiTestHelpers.Context()));
        var directEvents = await AiTestHelpers.Collect(
            BackendFixtures.Build(BackendFixtures.DirectApi, usage, tokens).Ask(AiTestHelpers.Context()));

        // Tokens equivalent.
        Assert.Equal(
            sidecarEvents.OfType<ChatTokenDelta>().Select(d => d.Text),
            directEvents.OfType<ChatTokenDelta>().Select(d => d.Text));

        // Completion text + usage equivalent.
        var sidecarDone = Assert.IsType<ChatCompleted>(sidecarEvents[^1]);
        var directDone = Assert.IsType<ChatCompleted>(directEvents[^1]);
        Assert.Equal(sidecarDone.Text, directDone.Text);
        Assert.Equal(sidecarDone.Usage, directDone.Usage);
    }
}

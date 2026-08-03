using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenario from docs/features/builtin-file-tools-sandbox/tests.md (SPEC §7.4): built-in tool
/// calls are surfaced inline in the conversation thread for transparency. Driven through the real
/// WPF window via FlaUI (UIA3), so this is tagged <c>Category=ui</c> and excluded from the headless
/// gate; it must compile and build. The conversation thread that hosts the tool activity is owned by
/// the M2 <c>conversations</c>/<c>streaming</c> features, so the helper that opens it throws a loud
/// <see cref="NotSupportedException"/> naming that owner until it lands — this test is never
/// silently green.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ToolTransparencyUiTests
{
    private readonly ShellUiFixture _fixture;

    public ToolTransparencyUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Tool calls appear inline in the thread
    //   Given the model used Read and Write during a turn
    //   Then the thread shows the tool activity for that turn
    [Fact]
    public void Tool_calls_appear_inline_in_the_thread()
    {
        var thread = OpenThread();

        // the thread shows the tool activity for that turn
        var toolActivity = thread.FindFirstDescendant(cf => cf.ByAutomationId("TurnToolActivity"));
        Assert.NotNull(toolActivity);

        // each tool call is visible with its name (Read and Write were used this turn)
        var readCall = toolActivity!.FindFirstDescendant(cf => cf.ByName("Read"));
        var writeCall = toolActivity.FindFirstDescendant(cf => cf.ByName("Write"));
        Assert.NotNull(readCall);
        Assert.NotNull(writeCall);
    }

    /// <summary>
    /// Opens the conversation thread that surfaces tool activity. The thread is owned by the M2
    /// conversations/streaming features; until they land there is nothing to drive, so this fails
    /// loudly rather than passing silently.
    /// </summary>
    private AutomationElement OpenThread()
    {
        var window = _fixture.MainWindow;
        return window.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"))
            ?? throw new NotSupportedException(
                "The conversation thread that surfaces built-in tool activity is owned by the " +
                "conversations/streaming (M2) features; wire this helper to the thread surface when it lands.");
    }
}

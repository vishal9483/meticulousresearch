namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// A transparency record of a single built-in tool call made during a turn (SPEC §7.4): the tool
/// name, the inputs it was called with, and the human-readable outcome. Every call is logged so it
/// can be surfaced inline in the conversation for the user.
/// </summary>
/// <param name="Tool">The built-in tool name (e.g. <c>Grep</c>).</param>
/// <param name="Inputs">The inputs the tool was called with, in order.</param>
/// <param name="Outcome">A human-readable summary of the outcome.</param>
/// <param name="Success">Whether the call succeeded (a rejected/failed call is recorded too).</param>
public sealed record ToolCallRecord(
    string Tool,
    IReadOnlyList<KeyValuePair<string, string>> Inputs,
    string Outcome,
    bool Success);

/// <summary>
/// Records every built-in tool call made during a turn so the conversation can show the tool
/// activity inline with its inputs and outcome (SPEC §7.4). The log is the transparency source
/// reused by <c>turn-metadata-actions</c> and later provenance features.
/// </summary>
public sealed class ToolCallLog
{
    private readonly List<ToolCallRecord> _calls = new();

    /// <summary>The tool calls recorded during the turn, in the order they were made.</summary>
    public IReadOnlyList<ToolCallRecord> Calls => _calls;

    /// <summary>Appends a tool-call record to the log.</summary>
    public void Record(ToolCallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _calls.Add(record);
    }
}

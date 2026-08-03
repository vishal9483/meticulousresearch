namespace MeticulousResearch.App.Tests;

/// <summary>
/// @manual checklist scenario from docs/features/command-palette-shortcuts/tests.md. A human pass
/// during PR review (SPEC §3.5 discoverability); tagged <c>Category=manual</c> and skipped in the gate.
/// </summary>
public class CommandPaletteManualTests
{
    // Scenario: Shortcuts are discoverable
    //   Given the app UI
    //   Then primary actions display their shortcut hint (tooltip or palette listing)
    //
    // Manual checklist:
    //   [ ] Press Ctrl+K — the command palette opens and each command lists its shortcut hint.
    //   [ ] Hover the primary actions (New project/conversation/artifact, Send, Stop, Search) —
    //       each shows its keyboard shortcut in a tooltip or in the palette listing.
    //   [ ] The hints match the actual bindings (Ctrl+K search, Ctrl+Enter send, Esc stop).
    [Fact(Skip = "@manual — shortcut-discoverability checklist, verified by a human during PR review.")]
    [Trait("Category", "manual")]
    public void Shortcuts_are_discoverable()
    {
    }
}

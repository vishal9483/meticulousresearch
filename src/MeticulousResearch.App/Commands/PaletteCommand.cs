namespace MeticulousResearch.App.Commands;

/// <summary>
/// A single invokable entry in the command palette (SPEC §3.5): a stable id, the display name shown
/// in the palette, optional search keywords, an optional discoverable shortcut hint (tooltip/palette
/// listing), and the delegate that performs the action when the command is chosen. Owned by the
/// <c>command-palette-shortcuts</c> feature; downstream features register their primary actions here
/// rather than inventing separate entry points.
/// </summary>
/// <param name="Id">Stable command identifier (e.g. <c>new-project</c> or <c>go-to-project:{id}</c>).</param>
/// <param name="DisplayName">Human-readable label shown in the palette and used for matching.</param>
/// <param name="Keywords">Additional terms the palette matches against (never <c>null</c>).</param>
/// <param name="Execute">The action invoked when the command is chosen.</param>
/// <param name="ShortcutHint">Optional keyboard shortcut hint (e.g. <c>Ctrl+K</c>) for discoverability.</param>
public sealed record PaletteCommand(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Keywords,
    Action Execute,
    string? ShortcutHint = null);

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// A single item in the shell's top-level navigation rail (SPEC §4). "Projects" is the root.
/// Later features may add further top-level roots (e.g. app Settings) without changing the shell.
/// </summary>
/// <param name="Key">Stable identifier for the item (e.g. "Projects").</param>
/// <param name="Label">Display label shown in the nav rail.</param>
public sealed record TopLevelNavItem(string Key, string Label);

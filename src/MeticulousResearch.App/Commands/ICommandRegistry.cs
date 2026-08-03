namespace MeticulousResearch.App.Commands;

/// <summary>
/// The catalog of invokable commands surfaced by the command palette (SPEC §3.5). Owns the static
/// core commands (New project / New conversation / New artifact / Search) plus the dynamic
/// "Go to project: {name}" entries built from the current project list. Owned by the
/// <c>command-palette-shortcuts</c> feature; downstream features register new primary actions here.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Returns the current set of commands: the core commands first, then a jump-to entry per
    /// project. Re-evaluated on each call so newly created projects appear immediately.
    /// </summary>
    IReadOnlyList<PaletteCommand> GetCommands();
}

namespace MeticulousResearch.Core.Theming;

/// <summary>
/// The theme a user can select (SPEC §3.7). <see cref="System"/> follows the operating-system
/// setting and resolves to either <see cref="Light"/> or <see cref="Dark"/> at runtime.
/// </summary>
public enum AppTheme
{
    /// <summary>The light palette.</summary>
    Light,

    /// <summary>The dark palette.</summary>
    Dark,

    /// <summary>Follow the OS setting; resolves to <see cref="Light"/> or <see cref="Dark"/>.</summary>
    System,
}

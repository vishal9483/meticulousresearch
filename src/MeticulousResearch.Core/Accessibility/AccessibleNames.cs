namespace MeticulousResearch.Core.Accessibility;

/// <summary>
/// The canonical accessible names for the app's primary controls, exposed as constants so the WPF
/// views bind <c>AutomationProperties.Name</c> to the very same strings that <c>@unit</c> tests
/// assert (accessibility/phase.md, SPEC §8). Keeping the names here means screen-reader labels are
/// verified without a window.
/// </summary>
public static class AccessibleNames
{
    /// <summary>Accessible name for the "create new project" button.</summary>
    public const string NewProjectButton = "New project";

    /// <summary>Accessible name for the Anthropic API-key input field.</summary>
    public const string ApiKeyField = "Anthropic API key";

    /// <summary>Accessible name for the model-selector control.</summary>
    public const string ModelSelector = "Model selector";

    /// <summary>Accessible name for the send-message button.</summary>
    public const string SendButton = "Send message";

    /// <summary>Accessible name for the stop-generation button.</summary>
    public const string StopButton = "Stop generation";

    /// <summary>Accessible name for the theme-selector control.</summary>
    public const string ThemeSelector = "Theme selector";

    /// <summary>Accessible name for the command-palette search box.</summary>
    public const string CommandPaletteSearchBox = "Command palette search";
}

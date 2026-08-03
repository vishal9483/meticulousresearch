namespace MeticulousResearch.Core.Accessibility;

/// <summary>
/// The registry of the app's primary controls and their accessibility metadata. Views must add
/// their primary controls here (and bind <c>AutomationProperties.Name</c> to the matching
/// <see cref="AccessibleNames"/> constant) so that accessible-name and label-association coverage is
/// asserted as <c>@unit</c> without a window (accessibility/phase.md, SPEC §8).
/// </summary>
public static class AccessibilityCatalog
{
    private static readonly IReadOnlyList<AccessibleControl> Controls = new[]
    {
        new AccessibleControl
        {
            Key = "New project button",
            Name = AccessibleNames.NewProjectButton,
            Kind = AccessibleControlKind.Button,
        },
        new AccessibleControl
        {
            Key = "API key field",
            Name = AccessibleNames.ApiKeyField,
            Kind = AccessibleControlKind.Input,
            AssociatedLabel = "API key",
        },
        new AccessibleControl
        {
            Key = "Model selector",
            Name = AccessibleNames.ModelSelector,
            Kind = AccessibleControlKind.Selector,
        },
        new AccessibleControl
        {
            Key = "Send button",
            Name = AccessibleNames.SendButton,
            Kind = AccessibleControlKind.Button,
            IsIconOnly = true,
        },
        new AccessibleControl
        {
            Key = "Stop button",
            Name = AccessibleNames.StopButton,
            Kind = AccessibleControlKind.Button,
            IsIconOnly = true,
        },
        new AccessibleControl
        {
            Key = "Theme selector",
            Name = AccessibleNames.ThemeSelector,
            Kind = AccessibleControlKind.Selector,
        },
        new AccessibleControl
        {
            Key = "Command palette search box",
            Name = AccessibleNames.CommandPaletteSearchBox,
            Kind = AccessibleControlKind.SearchBox,
        },
    };

    /// <summary>All primary controls the app guarantees accessible names for.</summary>
    public static IReadOnlyList<AccessibleControl> PrimaryControls => Controls;

    /// <summary>
    /// Looks up a primary control by its catalog <paramref name="key"/> (the Gherkin control label).
    /// </summary>
    /// <exception cref="KeyNotFoundException">No primary control has that key.</exception>
    public static AccessibleControl Get(string key)
    {
        foreach (var control in Controls)
        {
            if (control.Key == key)
            {
                return control;
            }
        }

        throw new KeyNotFoundException($"No primary control is registered with the key '{key}'.");
    }
}

namespace MeticulousResearch.Core.Accessibility;

/// <summary>The interaction kind of a primary control, used to drive accessibility guarantees.</summary>
public enum AccessibleControlKind
{
    /// <summary>A push button.</summary>
    Button,

    /// <summary>A text/secure input field.</summary>
    Input,

    /// <summary>A selector (combo box / choice control).</summary>
    Selector,

    /// <summary>A search box.</summary>
    SearchBox,
}

/// <summary>
/// A primary control described for accessibility: its catalog key (the display label used by the
/// Gherkin scenarios), its accessible <see cref="Name"/>, whether it is icon-only, and — for
/// inputs — the visible label a screen reader announces on focus (accessibility/tests.md, SPEC §8).
/// </summary>
public sealed record AccessibleControl
{
    /// <summary>The catalog key (matches the control label used in the Gherkin examples).</summary>
    public required string Key { get; init; }

    /// <summary>The accessible name exposed for automation/screen readers. Never empty.</summary>
    public required string Name { get; init; }

    /// <summary>The interaction kind of this control.</summary>
    public required AccessibleControlKind Kind { get; init; }

    /// <summary>True when the control renders only an icon (so its name must describe the action).</summary>
    public bool IsIconOnly { get; init; }

    /// <summary>For inputs, the visible label associated with the field; otherwise <c>null</c>.</summary>
    public string? AssociatedLabel { get; init; }
}

using MeticulousResearch.Core.Accessibility;

namespace MeticulousResearch.Core.Tests.Accessibility;

/// <summary>
/// @unit scenarios from docs/features/accessibility/tests.md covering screen-reader/automation
/// names on primary controls, icon-only buttons, and label association (SPEC §8).
/// </summary>
public class AccessibleNamesTests
{
    // Scenario Outline: Primary controls expose an accessible name
    //   Given the "<control>" on its screen
    //   Then it exposes a non-empty accessible name for automation/screen readers
    [Theory]
    [InlineData("New project button")]
    [InlineData("API key field")]
    [InlineData("Model selector")]
    [InlineData("Send button")]
    [InlineData("Stop button")]
    [InlineData("Theme selector")]
    [InlineData("Command palette search box")]
    public void Primary_controls_expose_an_accessible_name(string control)
    {
        var accessible = AccessibilityCatalog.Get(control);

        Assert.False(string.IsNullOrWhiteSpace(accessible.Name),
            $"the '{control}' control must expose a non-empty accessible name");
    }

    // Scenario: Icon-only buttons have an accessible name, not just an icon
    //   Given an icon-only button
    //   Then it exposes an accessible name describing its action
    [Fact]
    public void Icon_only_buttons_have_an_accessible_name_not_just_an_icon()
    {
        var iconOnly = AccessibilityCatalog.PrimaryControls
            .Where(c => c.IsIconOnly)
            .ToList();

        Assert.NotEmpty(iconOnly);

        foreach (var button in iconOnly)
        {
            // A name "describing its action" is a non-empty word, not a lone glyph/character.
            Assert.False(string.IsNullOrWhiteSpace(button.Name),
                $"icon-only '{button.Key}' must expose an accessible name");
            Assert.True(button.Name.Trim().Length > 1,
                $"icon-only '{button.Key}' name '{button.Name}' should describe its action");
            Assert.Contains(button.Name, ch => char.IsLetter(ch));
        }
    }

    // Scenario: Inputs are associated with their labels
    //   Given a labelled input field
    //   Then a screen reader announces the label when the field is focused
    [Fact]
    public void Inputs_are_associated_with_their_labels()
    {
        var inputs = AccessibilityCatalog.PrimaryControls
            .Where(c => c.Kind == AccessibleControlKind.Input)
            .ToList();

        Assert.NotEmpty(inputs);

        foreach (var input in inputs)
        {
            // The label a screen reader announces on focus is the associated label (or the
            // accessible name when the label is the name), and must be non-empty.
            var announced = input.AssociatedLabel ?? input.Name;
            Assert.False(string.IsNullOrWhiteSpace(announced),
                $"input '{input.Key}' must announce an associated label when focused");
        }
    }
}

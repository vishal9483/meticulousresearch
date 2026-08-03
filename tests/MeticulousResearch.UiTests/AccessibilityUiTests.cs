using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MeticulousResearch.Core.Accessibility;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/accessibility/tests.md — keyboard navigation, tab order, dialog
/// focus trap/restore, and focus visibility (SPEC §8, §3.7). These drive the real WPF window via
/// FlaUI (UIA3); tagged <c>Category=ui</c> so they are excluded from the headless gate, but they
/// must compile and build.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class AccessibilityUiTests
{
    private readonly ShellUiFixture _fixture;

    public AccessibilityUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Every primary screen is reachable and operable by keyboard alone
    //   Given the app is running
    //   When I navigate using only Tab, arrow keys, and Enter
    //   Then I can reach and activate the primary action on each main screen
    [Fact]
    public void Every_primary_screen_is_reachable_and_operable_by_keyboard_alone()
    {
        var window = _fixture.MainWindow;

        // The primary action of the home screen carries the shared accessible name; keyboard-only
        // users reach it by Tab and activate it with Enter.
        var primaryAction = window.FindFirstDescendant(
            cf => cf.ByName(AccessibleNames.NewProjectButton))?.AsButton();
        Assert.NotNull(primaryAction);

        // Tab to it, then activate with Enter — no mouse.
        primaryAction!.Focus();
        Assert.True(primaryAction.Properties.HasKeyboardFocus.ValueOrDefault);
        Keyboard.Press(VirtualKeyShort.ENTER);

        // The primary action responded to keyboard activation (its destination view is present).
        var destination = window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectDialogRoot"));
        Assert.NotNull(destination);
    }

    // Scenario: Tab order on a screen follows a logical reading order
    //   Given the Settings screen is open
    //   When I press Tab repeatedly from the top
    //   Then focus moves through the controls in a logical top-to-bottom order
    //   And focus does not get trapped on any control
    [Fact]
    public void Tab_order_on_a_screen_follows_a_logical_reading_order()
    {
        var window = _fixture.MainWindow;

        var settingsRoot = window.FindFirstDescendant(cf => cf.ByAutomationId("AppSettingsRoot"));
        Assert.NotNull(settingsRoot);

        // Focus the first control on the screen, then Tab down through it.
        var first = window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsFirstControl"));
        Assert.NotNull(first);
        first!.Focus();

        var previousTop = ControlTop(first);
        var previouslyFocused = first;
        var seen = new HashSet<string> { AutomationIdOf(first) };

        for (var i = 0; i < 8; i++)
        {
            Keyboard.Press(VirtualKeyShort.TAB);
            var focused = window.FindFirstDescendant(cf => cf.ByAutomationId("AppSettingsRoot"))!
                .Automation.FocusedElement();
            Assert.NotNull(focused);

            // Focus does not get trapped: each Tab moves to a different control.
            Assert.NotEqual(AutomationIdOf(previouslyFocused), AutomationIdOf(focused));

            // Logical top-to-bottom order: focus never jumps upward past where it started.
            var top = ControlTop(focused);
            Assert.True(top >= previousTop - 1,
                $"tab order jumped upward (from {previousTop} to {top}); order is not top-to-bottom");

            previousTop = top;
            previouslyFocused = focused;
            if (!seen.Add(AutomationIdOf(focused)))
            {
                // Wrapped back to a seen control — a full, non-trapping cycle exists.
                break;
            }
        }
    }

    // Scenario: Dialogs trap focus while open and restore it on close
    //   Given a modal dialog is open
    //   When I Tab past the last control
    //   Then focus wraps within the dialog
    //   And on closing the dialog focus returns to the control that opened it
    [Fact]
    public void Dialogs_trap_focus_while_open_and_restore_it_on_close()
    {
        var window = _fixture.MainWindow;

        // The control that opens the dialog — remember it to assert focus restoration.
        var opener = window.FindFirstDescendant(
            cf => cf.ByName(AccessibleNames.NewProjectButton))?.AsButton();
        Assert.NotNull(opener);
        var openerId = AutomationIdOf(opener!);
        opener!.Focus();
        opener.Invoke();

        var dialog = window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectDialogRoot"));
        Assert.NotNull(dialog);

        // Focus the dialog's last control, then Tab past it — focus wraps inside the dialog.
        var lastControl = window.FindFirstDescendant(cf => cf.ByAutomationId("DialogLastControl"));
        Assert.NotNull(lastControl);
        lastControl!.Focus();
        Keyboard.Press(VirtualKeyShort.TAB);

        var afterWrap = window.Automation.FocusedElement();
        Assert.NotNull(afterWrap);
        // The wrapped focus is still a descendant of the dialog (focus is trapped).
        var dialogAfterWrap = window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectDialogRoot"));
        Assert.NotNull(dialogAfterWrap);
        Assert.NotNull(dialogAfterWrap!.FindFirstDescendant(
            cf => cf.ByAutomationId(AutomationIdOf(afterWrap))));

        // Close the dialog — focus returns to the control that opened it.
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        var restored = window.Automation.FocusedElement();
        Assert.NotNull(restored);
        Assert.Equal(openerId, AutomationIdOf(restored));
    }

    // Scenario: The focused control shows a visible focus indicator
    //   Given I move focus to a control with the keyboard
    //   Then a visible focus indicator is shown on that control
    [Fact]
    public void The_focused_control_shows_a_visible_focus_indicator()
    {
        var window = _fixture.MainWindow;

        var control = window.FindFirstDescendant(
            cf => cf.ByName(AccessibleNames.NewProjectButton))?.AsButton();
        Assert.NotNull(control);

        // Move focus with the keyboard.
        control!.Focus();
        Assert.True(control.Properties.HasKeyboardFocus.ValueOrDefault,
            "the control must actually hold keyboard focus");

        // The shared focus adorner renders for the focused control (its AutomationId is exposed by
        // the design-system FocusVisualStyle when a control is keyboard-focused).
        var adorner = window.FindFirstDescendant(cf => cf.ByAutomationId("FocusAdorner"));
        Assert.NotNull(adorner);
        Assert.False(adorner!.IsOffscreen, "the focus indicator must be visible on screen");
    }

    private static double ControlTop(AutomationElement element)
        => element.Properties.BoundingRectangle.ValueOrDefault.Top;

    private static string AutomationIdOf(AutomationElement element)
        => element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
}

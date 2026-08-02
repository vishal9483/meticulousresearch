# Tests — Accessibility

**SPEC:** §8 (accessibility). **Milestone:** M5.
**Depends on:** design-system-theming

## Traceability
- §8 keyboard-navigable → Keyboard-navigation + tab-order scenarios.
- §8 screen-reader labels on primary controls → Automation-label scenarios.
- §8 WCAG-AA contrast in both themes → Contrast scenarios (broadens `design-system-theming`'s check).
- §3.7 focus visible → Focus-visibility scenarios.
- §9.1(10) no unstyled/placeholder screens → Consistency (labels present on every primary control).

---

```gherkin
Feature: Accessibility
  As an analyst who relies on the keyboard and a screen reader
  I want every primary control labelled, reachable, and legible
  So that I can operate the whole app without a mouse and read it in either theme
```

### Keyboard navigation & tab order

```gherkin
@ui
Scenario: Every primary screen is reachable and operable by keyboard alone
  Given the app is running
  When I navigate using only Tab, arrow keys, and Enter
  Then I can reach and activate the primary action on each main screen

@ui
Scenario: Tab order on a screen follows a logical reading order
  Given the Settings screen is open
  When I press Tab repeatedly from the top
  Then focus moves through the controls in a logical top-to-bottom order
  And focus does not get trapped on any control

@ui
Scenario: Dialogs trap focus while open and restore it on close
  Given a modal dialog is open
  When I Tab past the last control
  Then focus wraps within the dialog
  And on closing the dialog focus returns to the control that opened it
```

### Focus visibility

```gherkin
@ui
Scenario: The focused control shows a visible focus indicator
  Given I move focus to a control with the keyboard
  Then a visible focus indicator is shown on that control

@manual
Scenario: The focus indicator is visible in both themes
  Given the app in Light and in Dark theme
  When I keyboard-focus controls
  Then the focus indicator is clearly visible against the surface in each theme
```

### Screen-reader / automation labels

```gherkin
@unit
Scenario Outline: Primary controls expose an accessible name
  Given the "<control>" on its screen
  Then it exposes a non-empty accessible name for automation/screen readers

  Examples:
    | control                      |
    | New project button           |
    | API key field                |
    | Model selector               |
    | Send button                  |
    | Stop button                  |
    | Theme selector               |
    | Command palette search box   |

@unit
Scenario: Icon-only buttons have an accessible name, not just an icon
  Given an icon-only button
  Then it exposes an accessible name describing its action

@unit
Scenario: Inputs are associated with their labels
  Given a labelled input field
  Then a screen reader announces the label when the field is focused
```

### Contrast (both themes)

```gherkin
@unit
Scenario Outline: Text and interactive controls meet WCAG-AA contrast in each theme
  Given the "<theme>" theme tokens
  Then body text on its surface has a contrast ratio of at least 4.5:1
  And primary button text on its fill has a contrast ratio of at least 4.5:1
  And the focus indicator meets at least 3:1 against its surface

  Examples:
    | theme |
    | Light |
    | Dark  |

@manual
Scenario: A full screen-reader pass reads the primary workflow coherently
  Given a screen reader is active
  When I walk the create-project → add-resource → conversation flow
  Then each control is announced with a meaningful name and role
  And nothing is announced as unlabelled or "pane"
```

# Tests — Design System & Theming

**SPEC:** §3.7 (visual identity & design system). **Milestone:** M0.
**Depends on:** app-shell-navigation

## Traceability
- §3.7 navy palette, light/dark/system themes → Theme scenarios.
- §3.7 styled Fluent control kit; no unstyled default WPF chrome → Styling scenarios.
- §8 accessibility (WCAG-AA contrast both themes) → Contrast scenario (deeper pass in `accessibility`).
- §9.1(10) no unstyled/placeholder screens → Consistency scenario.

---

```gherkin
Feature: Design system & theming
  As an analyst
  I want a coherent, professional look with light and dark themes
  So that the app reads as finished commercial software
```

### Theme switching

```gherkin
@unit
Scenario Outline: Selecting a theme sets the active theme
  Given the app is running
  When I set the theme to "<theme>"
  Then the active theme resolves to "<resolved>"

  Examples:
    | theme  | resolved            |
    | Light  | Light               |
    | Dark   | Dark                |
    | System | follows OS setting  |

@unit
Scenario: Theme choice persists across restart
  Given I set the theme to "Dark"
  When the app restarts
  Then the active theme is "Dark"

@ui
Scenario: Switching theme updates the UI live
  Given the app is showing the Light theme
  When I switch to the Dark theme
  Then the window surfaces and text update to dark styling without a restart
```

### Brand tokens & styled controls

```gherkin
@unit
Scenario: The brand palette exposes the required design tokens
  Given the design system resource dictionary is loaded
  Then it defines a primary navy color token
  And a single accent token
  And neutral surface tokens
  And semantic success, warning, and error tokens

@ui
Scenario Outline: Common controls use the styled kit, not default WPF chrome
  Given any screen using a "<control>"
  Then the control uses the app's styled template (not the OS default appearance)

  Examples:
    | control  |
    | Button   |
    | TextBox  |
    | ComboBox |
    | DataGrid |
    | Dialog   |
    | Toast    |
```

### Consistency & contrast

```gherkin
@manual
Scenario: Both themes present a coherent navy-based identity
  Given the app in Light and in Dark theme
  Then surfaces, text, and accents form a coherent professional palette in each
  And no screen shows unstyled default WPF chrome

@unit
Scenario Outline: Core text/background pairs meet WCAG-AA contrast
  Given the "<theme>" theme tokens
  Then the body text on primary surface contrast ratio is at least 4.5:1
  Examples:
    | theme |
    | Light |
    | Dark  |
```

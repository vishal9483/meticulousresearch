# Tests — About Screen

**SPEC:** §3.7 (app identity, About screen with version; app icon). **Milestone:** M5.
**Depends on:** app-shell-navigation

## Traceability
- §3.7 app identity: application icon, product name → Identity scenarios.
- §3.7 About screen with version → Version scenarios.
- §4(7) Settings → About/version → Navigation scenario.
- §9.1(10) no unstyled/placeholder screens → styled About screen.

---

```gherkin
Feature: About screen
  As an analyst
  I want an About screen showing the app's identity and version
  So that I know what I'm running and can report it when needed
```

### App identity

```gherkin
@unit
Scenario: The About screen shows the product name
  Given the About screen
  Then it displays the product name "MeticulousResearch Desktop"

@unit
Scenario: The About screen shows the app icon
  Given the About screen
  Then it displays the application icon
```

### Version

```gherkin
@unit
Scenario: The About screen shows the application version
  Given the running app reports a version
  When I open the About screen
  Then it displays that version

@unit
Scenario: The displayed version comes from the assembly, not a hard-coded string
  Given the app's assembly version is "1.0.0"
  When I read the version shown on the About screen
  Then it equals the assembly's informational version
```

### Navigation & presentation

```gherkin
@ui
Scenario: The About screen is reachable from Settings
  Given the Settings screen is open
  When I choose "About"
  Then the About screen is shown

@ui
Scenario: The About screen is closable and returns to where it opened
  Given the About screen is open
  When I close it
  Then I return to the previous screen

@manual
Scenario: The About screen is branded and styled
  Given the About screen
  Then it presents app icon, product name, and version in the app's styled design
  And shows no unstyled default WPF chrome
```

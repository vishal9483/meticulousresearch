# Tests — App Branding & Icon

**SPEC:** §3.7 (visual identity: app identity — application icon, product name, installer branding). **Milestone:** M6.
**Depends on:** design-system-theming

## Traceability
- §3.7 application icon (window / taskbar / installer) → Icon scenarios.
- §3.7 product name shown consistently → Product-name scenarios.
- §3.7 coherent brand identity in the packaged app → Consistency scenario.
- §3.7 installer branding → Installer-branding scenario (installer mechanics live in `installer`).
- §9.1(1) branded first-run onboarding → Branded-launch scenario.
- §9.1(10) no unstyled/placeholder screens → Consistency scenario.

---

```gherkin
Feature: App branding & icon
  As an analyst
  I want a consistent product name and icon everywhere the app appears
  So that it reads as a single, finished, trustworthy product
```

### Application icon

```gherkin
@unit
Scenario: The app ships a multi-resolution application icon
  Given the app's icon asset
  Then it provides the standard Windows icon sizes (16, 32, 48, 256)
  And it is the icon referenced by the executable

@ui
Scenario: The main window shows the app icon
  Given the app is running
  When I look at the main window title bar
  Then it shows the MeticulousResearch application icon

@manual
Scenario: The taskbar and Start Menu show the app icon
  Given the app is installed and running
  Then the taskbar entry shows the application icon
  And the Start Menu entry shows the same icon
```

### Product name

```gherkin
@unit
Scenario: The product name is defined once and reused
  Given the app's branding metadata
  Then the product name resolves to "MeticulousResearch Desktop"
  And the window title, About screen, and package metadata all read from that single source

@ui
Scenario: The main window title carries the product name
  Given the app is running
  Then the window title includes "MeticulousResearch"

@manual
Scenario: The installer displays the product name and icon
  Given the signed installer
  When I run it
  Then it displays the product name "MeticulousResearch Desktop"
  And it shows the application icon in its branding
```

### Brand consistency

```gherkin
@ui
Scenario: First-run onboarding is branded
  Given a first launch
  When the onboarding welcome step appears
  Then it shows the product name and brand identity (navy palette, app icon/logo)
  And it does not show a placeholder or default WPF chrome

@manual
Scenario: Brand identity is coherent across the packaged app
  Given the installed, packaged app
  Then the icon, product name, and navy brand palette are consistent across the title bar, taskbar, onboarding, About screen, and installer
  And nothing shows a generic default icon or a wrong/placeholder name
```

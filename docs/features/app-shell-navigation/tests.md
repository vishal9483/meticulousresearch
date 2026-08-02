# Tests — App Shell & Navigation

**SPEC:** §4 (screen inventory), §7.1 (WPF/MVVM). **Milestone:** M0.
**Depends on:** —

## Traceability
- §4.2 three-pane project workspace → Scenarios: Workspace layout, Nav switches center pane.
- §4.1 Home/Projects list is the landing screen → Scenario: App opens to Projects home.
- §9.1(10) no placeholder screens → Scenario: Every nav destination renders a real view.

---

```gherkin
Feature: Application shell & navigation
  As an analyst
  I want a coherent window with predictable navigation
  So that I can move between projects and their views without dead ends
```

### Shell startup

```gherkin
@ui
Scenario: App opens to the Projects home
  Given the application has completed first-run setup
  When I launch the application
  Then the main window is shown maximized-restorable with the product title "MeticulousResearch"
  And the Projects home view is the active view

@unit
Scenario: Shell exposes the primary navigation regions
  Given the main shell view-model is initialized
  Then it exposes a top-level navigation with "Projects" as the root
  And a content region bound to the current view-model
```

### Project workspace three-pane layout

```gherkin
@ui
Scenario: Opening a project shows the three-pane workspace
  Given a project "Semiconductors 2026" exists
  When I open the project
  Then a left pane lists "Conversations", "Resources", "Artifacts", "Dashboard", "Settings"
  And a center pane shows the project's default view
  And a right contextual pane is present but may be empty

@ui
Scenario Outline: Left-nav switches the center pane
  Given I have the project "Semiconductors 2026" open
  When I select "<section>" in the left nav
  Then the center pane shows the "<section>" view
  And the selected nav item is visually marked active

  Examples:
    | section       |
    | Conversations |
    | Resources     |
    | Artifacts     |
    | Dashboard     |
```

### View-model navigation contract (drives the UI)

```gherkin
@unit
Scenario: Navigating sets the current view-model
  Given a shell view-model with a registered navigation service
  When I navigate to the "Resources" section of a project
  Then the shell's CurrentViewModel is a ResourcesViewModel scoped to that project

@unit
Scenario: Navigating to a project the shell records it as active
  Given a shell view-model
  When I navigate into project with id "P1"
  Then the shell's ActiveProjectId is "P1"

@unit
Scenario: Back navigation returns to the previous view
  Given I navigated Projects home -> project "P1" -> Resources
  When I invoke back navigation
  Then the current view is the "P1" workspace default view
```

### No dead ends

```gherkin
@ui
Scenario Outline: Every navigation destination renders a real view (no placeholders)
  Given the application is running
  When I navigate to "<destination>"
  Then a designed view is rendered
  And no "Not implemented" or blank placeholder is shown

  Examples:
    | destination        |
    | Projects home      |
    | Project dashboard  |
    | Conversations      |
    | Resources          |
    | Artifacts          |
    | Settings           |

@ui
Scenario: Window is resizable without breaking the layout
  Given a project workspace is open
  When I resize the window to a narrow width (1024px)
  Then the three panes remain usable
  And no content is clipped beyond scroll regions
```

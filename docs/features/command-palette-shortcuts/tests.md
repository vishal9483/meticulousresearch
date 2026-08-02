# Tests — Command Palette & Keyboard Shortcuts

**SPEC:** §3.5 (command palette, keyboard shortcuts). **Milestone:** M5.
**Depends on:** app-shell-navigation

## Traceability
- §3.5 Ctrl+K command palette to jump to project / new conversation / new artifact / search → Palette scenarios.
- §3.5 keyboard shortcuts: new project/conversation/artifact, send (Ctrl+Enter), stop (Esc), search (Ctrl+K) → Shortcut scenarios.
- §8 keyboard-navigable → Focus/keyboard scenarios (deeper pass in `accessibility`).
- §9.1(10) no dead ends → palette always offers actionable results / empty state.

---

```gherkin
Feature: Command palette & keyboard shortcuts
  As an analyst
  I want a Ctrl+K palette and consistent shortcuts
  So that I can jump anywhere and act without reaching for the mouse
```

### Opening & dismissing the palette

```gherkin
@ui
Scenario: Ctrl+K opens the command palette
  Given the app is running
  When I press Ctrl+K
  Then the command palette is shown with focus in its search box

@ui
Scenario: Esc closes the command palette
  Given the command palette is open
  When I press Esc
  Then the palette is dismissed
  And focus returns to where it was
```

### Palette search & commands

```gherkin
@unit
Scenario: The palette lists the core commands when empty
  Given the command palette is open with no query
  Then it offers "New project"
  And "New conversation"
  And "New artifact"
  And "Search"

@unit
Scenario Outline: Typing filters commands and jump targets
  Given a project named "Semiconductors 2026" exists
  And the command palette is open
  When I type "<query>"
  Then the top result is "<result>"

  Examples:
    | query        | result                       |
    | new conv     | New conversation             |
    | semic        | Go to project: Semiconductors 2026 |
    | search       | Search                       |

@unit
Scenario: Selecting a jump-to-project result navigates to that project
  Given a project named "Energy 2026" exists
  And the palette shows "Go to project: Energy 2026"
  When I choose it
  Then the app navigates to the "Energy 2026" project workspace

@unit
Scenario Outline: Selecting a create command invokes that action
  Given the command palette is open
  When I choose "<command>"
  Then the "<action>" is invoked

  Examples:
    | command          | action                    |
    | New project      | create a new project      |
    | New conversation | create a new conversation |
    | New artifact     | create a new artifact     |
    | Search           | open search               |

@unit
Scenario: A query with no matches shows a designed empty result state
  Given the command palette is open
  When I type a query that matches nothing
  Then I see a "No matching commands" empty state
  And no raw error is shown

@ui
Scenario: Arrow keys and Enter drive the palette from the keyboard
  Given the command palette is open with multiple results
  When I press the down arrow and then Enter
  Then the highlighted result is activated
```

### Global keyboard shortcuts

```gherkin
@ui
Scenario Outline: Global shortcuts invoke their action
  Given the app is on a screen where "<shortcut>" applies
  When I press "<shortcut>"
  Then the "<action>" is invoked

  Examples:
    | shortcut   | action                    |
    | Ctrl+K     | open the command palette  |
    | Ctrl+Enter | send the composed message |
    | Esc        | stop the active generation|

@unit
Scenario: Ctrl+Enter in the composer sends the message
  Given the conversation composer has text
  When I trigger the send shortcut
  Then the message is sent

@unit
Scenario: Esc during a streaming generation stops it
  Given a generation is streaming
  When I trigger the stop shortcut
  Then the generation is cancelled

@unit
Scenario: Esc does nothing destructive when nothing is streaming
  Given no generation is in progress
  When I trigger the stop shortcut
  Then no action is taken and no error is shown

@manual
Scenario: Shortcuts are discoverable
  Given the app UI
  Then primary actions display their shortcut hint (tooltip or palette listing)
```

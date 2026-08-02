# Tests — Projects CRUD

**SPEC:** §3.1 (projects). **Milestone:** M0.
**Depends on:** data-store-migrations, app-shell-navigation

## Traceability
- §3.1 CRUD (create/rename/duplicate/archive/delete) → CRUD scenarios.
- §3.1 fields (name, description, custom instructions, default model, color, archived, timestamps) → Fields scenario.
- §3.1 dashboard (counts, last activity, quick actions) → Dashboard scenarios.
- §3.1 search (filter by name/description) → Search scenario.
- §9.1(2) create a project → covered here (from-template creation lives in deliverable-templates).

---

```gherkin
Feature: Research projects
  As an analyst
  I want to create and manage research projects
  So that each piece of work is a self-contained, provenance-intact workspace
```

### Create

```gherkin
@unit
Scenario: Creating a blank project with a name
  Given the Projects home is open
  When I create a project named "Automotive EV 2026"
  Then a project "Automotive EV 2026" exists
  And its created_at and updated_at are set
  And its default model is the app default "claude-opus-5"
  And it is not archived

@unit
Scenario: Project name is required
  Given the new-project form is open
  When I try to create a project with an empty name
  Then I see an inline validation error
  And no project is created

@ui
Scenario: A newly created project opens to its workspace
  Given the Projects home is open
  When I create a project named "Food & Beverage 2026"
  Then the project workspace for "Food & Beverage 2026" is shown
```

### Fields & custom instructions

```gherkin
@unit
Scenario: A project stores all specified fields
  Given a new project
  When I set description "10-year EV forecast", custom instructions "Use house style, formal tone", default model "claude-sonnet-5", and color "navy"
  Then those values persist and are re-read after reopening the project

@unit
Scenario: Custom instructions are available for grounding
  Given a project with custom instructions "Always cite sources"
  When the project's system-prompt context is assembled
  Then it includes "Always cite sources"
```

### Rename / duplicate / archive / delete

```gherkin
@unit
Scenario: Renaming a project updates its name and timestamp
  Given a project named "Semiconductors 2026"
  When I rename it to "Semiconductors 2027"
  Then the project is named "Semiconductors 2027"
  And its updated_at is newer than before

@unit
Scenario: Duplicating a project copies its configuration and resources
  Given a project "Base Study" with 2 resources and custom instructions
  When I duplicate it as "Base Study (copy)"
  Then a new project "Base Study (copy)" exists
  And it has the same custom instructions and default model
  And it has copies of the 2 resources
  And conversations and artifacts are NOT copied

@unit
Scenario: Archiving hides a project from the default list
  Given an active project "Old Study"
  When I archive it
  Then it does not appear in the default Projects home list
  And it appears when the "Show archived" toggle is on

@unit
Scenario: Unarchiving restores a project to the default list
  Given an archived project "Old Study"
  When I unarchive it
  Then it appears in the default Projects home list

@unit
Scenario: Deleting a project removes it and its files
  Given a project "Scratch" with resources on disk
  When I delete it and confirm
  Then the project no longer exists
  And its "projects/{id}" directory is removed

@ui
Scenario: Deleting a project asks for confirmation
  Given a project "Scratch"
  When I choose Delete
  Then I am asked to confirm before anything is deleted
```

### Dashboard & search

```gherkin
@unit
Scenario: Project dashboard reports counts and last activity
  Given a project with 3 resources, 2 conversations, and 1 artifact
  When I view the project dashboard
  Then it shows resource count 3, conversation count 2, artifact count 1
  And it shows the most recent activity timestamp

@ui
Scenario: Dashboard quick actions are present
  Given a project dashboard is open
  Then quick actions "New conversation", "Add resource", and "New artifact" are available

@unit
Scenario Outline: Searching projects filters by name or description
  Given projects "Healthcare 2026", "Energy 2026", and "Automotive 2025"
  When I search projects for "<query>"
  Then the results are "<results>"

  Examples:
    | query      | results                        |
    | 2026       | Healthcare 2026, Energy 2026   |
    | Automotive | Automotive 2025                |
    | zzz        |                                |

@ui
Scenario: Empty projects list shows a designed empty state
  Given there are no projects
  When I open the Projects home
  Then I see an empty state with a "create your first research project" call to action
```

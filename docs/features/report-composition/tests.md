# Tests — Report Composition

**SPEC:** §3.4.1 (compose a full report from ordered artifacts). **Milestone:** M3.
**Depends on:** artifact-creation

## Traceability
- §3.4.1 order multiple artifacts into a single report compilation → Composition scenarios.
- §3.4.1 the compilation is a document artifact that references sections in order → Model scenarios.
- §3.4.1 exported later as one branded file → Export-handoff scenarios (export itself is `branded-export`, M4).
- §9.1(6) export a branded client-ready report assembled from sections → composition is the ordered source that export consumes.

---

```gherkin
Feature: Report composition
  As an analyst
  I want to order several artifacts into a single report
  So that sections assembled separately export as one cohesive, branded deliverable
```

Background: a project with several section artifacts.

```gherkin
Background:
  Given a project "Grid Storage 2026" with artifacts:
    | title                | type    |
    | Executive Summary    | doc     |
    | Market Sizing        | doc     |
    | Forecast Table       | table   |
    | Competitive Landscape| table   |
```

### Creating and ordering a composition

```gherkin
@unit
Scenario: Creating a report composition produces a document artifact
  When I create a report composition titled "Grid Storage 2026 — Full Report"
  Then a document artifact "Grid Storage 2026 — Full Report" exists
  And it is marked as a report composition

@unit
Scenario: Adding artifacts to a composition records them as ordered section references
  Given a report composition
  When I add "Executive Summary", then "Market Sizing", then "Forecast Table"
  Then the composition references those three artifacts in that order
  And it references them (does not copy their content)

@unit
Scenario: Reordering sections changes the composition order
  Given a composition ordered Executive Summary, Market Sizing, Forecast Table
  When I move "Forecast Table" above "Market Sizing"
  Then the order is Executive Summary, Forecast Table, Market Sizing

@unit
Scenario: Removing a section drops it from the composition but not the project
  Given a composition containing "Market Sizing"
  When I remove "Market Sizing" from the composition
  Then the composition no longer references "Market Sizing"
  And the "Market Sizing" artifact still exists in the project
```

### References track their source artifacts

```gherkin
@unit
Scenario: A section reflects its source artifact's current version
  Given a composition referencing "Market Sizing" at its current version
  When "Market Sizing" gets a new current version via Edit with Claude
  Then the composition's rendered section reflects the new current version

@unit
Scenario: A composition can pin a section to a specific version
  Given a composition referencing "Forecast Table"
  When I pin that section to version 2
  Then the section renders version 2 even after "Forecast Table" advances to version 3
```

### Rendering the compiled document

```gherkin
@unit
Scenario: The compiled document concatenates sections in order
  Given a composition ordered Executive Summary, Market Sizing, Forecast Table
  When I render the composition
  Then the compiled content contains each section's content in that order

@unit
Scenario: Each section carries its artifact title as a heading
  Given a composition with a "Market Sizing" section
  When I render the composition
  Then the compiled document includes a "Market Sizing" section heading

@unit
Scenario: A table section renders as a table within the document
  Given a composition containing the "Forecast Table" table artifact
  When I render the composition
  Then the table's rows appear as a table in the compiled document
```

### Validation & edge cases

```gherkin
@unit
Scenario: A section referencing a deleted artifact is flagged
  Given a composition referencing "Competitive Landscape"
  When the "Competitive Landscape" artifact is deleted
  Then the composition flags a broken section reference
  And rendering skips it with a visible placeholder note

@unit
Scenario: An empty composition renders an empty document with guidance
  Given a report composition with no sections
  When I render it
  Then the compiled document is empty
  And the composition view prompts me to add sections
```

### Export hand-off (§3.4.1 / §9.1(6))

```gherkin
@unit
Scenario: A composition exposes its ordered sections for export
  Given a composition ordered Executive Summary, Market Sizing, Forecast Table
  When export requests the composition's content
  Then it receives the sections in composition order as a single document
```

### UI

```gherkin
@ui
Scenario: The report composition view lists sections in order with drag-to-reorder
  Given a composition with three sections
  When I open the report composition view
  Then the sections are listed in order
  And I can drag a section to reorder it

@ui
Scenario: The composition view offers adding an artifact as a section
  Given a report composition view is open
  Then an "Add section" action lets me pick an existing project artifact
```

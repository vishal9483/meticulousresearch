# Tests — Full-Text Search

**SPEC:** §3.1 (full-text search within a project via SQLite FTS5), §5 (FTS5 virtual tables). **Milestone:** M1.
**Depends on:** text-paste-resource

## Traceability
- §3.1 full-text search within a project across resources → Search scenarios.
- §5 FTS5 virtual tables over resource extracted text → Index scenarios.
- §3.1 (later) search also covers conversations & artifacts → Extensibility scenario (noted, deferred).
- §9.1(3) resources are added/extracted (searchable) → covered here.

> **Scope note:** M1 implements FTS over **resource extracted text** within a project. The FTS5
> tables for **message content** and **artifact version content** already exist (owned by
> data-store-migrations §5); wiring search UI over conversations/artifacts arrives with M2/M3.
> One scenario asserts this feature is designed to extend to those content types.

---

```gherkin
Feature: Full-text search within a project
  As an analyst
  I want to search across a project's resources
  So that I can quickly find the source material I need
```

```gherkin
Background:
  Given a project "Semiconductors 2026" is open
  And it has resources:
    | title          | extracted text                                   |
    | Foundry note   | Global foundry capacity grew 12% in 2025.        |
    | Wafer note     | Wafer starts rose sharply across leading nodes.  |
    | Pricing memo   | ASP declined in mature nodes during 2025.        |
```

### Searching resources

```gherkin
@unit @integration
Scenario: A keyword search returns matching resources
  When I search the project for "foundry"
  Then the results include "Foundry note"
  And they exclude "Wafer note" and "Pricing memo"

@unit @integration
Scenario: Search matches across the extracted text, not just the title
  When I search the project for "wafer"
  Then the results include "Wafer note"

@unit @integration
Scenario Outline: Search ranks and filters by relevance
  When I search the project for "<query>"
  Then the results are "<results>"

  Examples:
    | query   | results                      |
    | 2025    | Foundry note, Pricing memo   |
    | nodes   | Wafer note, Pricing memo     |
    | tin     |                              |

@unit @integration
Scenario: Search is case-insensitive
  When I search the project for "FOUNDRY"
  Then the results include "Foundry note"

@unit @integration
Scenario: Search is scoped to the current project only
  Given another project with a resource containing "foundry"
  When I search project "Semiconductors 2026" for "foundry"
  Then results come only from "Semiconductors 2026"
```

### Index maintenance

```gherkin
@unit @integration
Scenario: A newly added resource becomes searchable
  When I add a resource with text "Export controls tightened in 2025."
  And I search the project for "export"
  Then the new resource is in the results

@unit @integration
Scenario: Re-extracting updates what the resource matches
  Given a file resource currently matching "draft"
  When its re-extracted text no longer contains "draft" but contains "final"
  And I search for "final"
  Then the resource is in the results
  And searching for "draft" no longer returns it

@unit @integration
Scenario: Removing a resource drops it from the index
  Given a resource matching "obsolete"
  When I remove it
  And I search for "obsolete"
  Then no results are returned
```

### Empty & extensibility

```gherkin
@unit
Scenario: A query with no matches returns an empty result set
  When I search the project for "nonexistentterm"
  Then no results are returned

@unit @integration
Scenario: The search service is designed to extend to conversations and artifacts
  Given FTS tables exist for message content and artifact version content
  Then the search service can query those content types under the same project scope

@ui
Scenario: Searching from the resources view filters the list
  Given the Resources view is open
  When I type "foundry" in the search box
  Then only matching resources remain visible
  And a designed empty state shows when nothing matches
```

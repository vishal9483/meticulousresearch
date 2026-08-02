# Tests — Artifact Diff

**SPEC:** §3.4 (diff between any two versions). **Milestone:** M3.
**Depends on:** artifact-versioning

## Traceability
- §3.4 side-by-side / inline diff between any two versions → Diff-mode scenarios.
- §3.4 diff between *any* two versions (not just adjacent) → Arbitrary-pair scenarios.
- §9.1(5) compare versions (as part of the flagship report iteration flow) → Compare scenarios.

---

```gherkin
Feature: Artifact diff
  As an analyst
  I want to compare any two versions of an artifact side-by-side or inline
  So that I can see exactly what an edit or regeneration changed before I keep it
```

Background: an artifact with a known version history.

```gherkin
Background:
  Given an artifact "Executive Summary" with versions:
    | version | content            |
    | 1       | The market is $2B. |
    | 2       | The market is $3B. |
    | 3       | The market is $3B and growing. |
```

### Computing a diff

```gherkin
@unit
Scenario: Diffing two versions reports the changed lines
  When I diff version 1 against version 2
  Then the diff marks "The market is $2B." as removed
  And marks "The market is $3B." as added

@unit
Scenario: Diffing identical content reports no changes
  Given two versions with identical content
  When I diff them
  Then the diff reports no differences

@unit
Scenario: Diffing additive-only changes marks only additions
  When I diff version 2 against version 3
  Then "and growing" is marked as added
  And nothing is marked as removed
```

### Any two versions (not just adjacent)

```gherkin
@unit
Scenario: Non-adjacent versions can be compared
  When I diff version 1 against version 3
  Then the diff reflects all changes from version 1 to version 3

@unit
Scenario: Diff direction is respected (old → new)
  When I select version 3 as the base and version 1 as the compare
  Then "and growing" is marked as removed
```

### Diff presentation modes

```gherkin
@ui
Scenario: Side-by-side diff shows both versions in parallel panes
  Given the artifact editor is in diff mode
  When I compare version 1 and version 2
  Then version 1 is shown in the left pane and version 2 in the right pane
  And changed regions are highlighted in both

@ui
Scenario: Inline diff shows changes in a single merged view
  Given the artifact editor is in diff mode
  When I switch to inline view
  Then removals and additions are shown interleaved in one pane

@ui
Scenario: The version pickers default to comparing the previous version against the current
  Given an artifact with 3 versions, version 3 current
  When I open diff mode
  Then version 2 is preselected as base and version 3 as compare
```

### Format-aware diffing

```gherkin
@unit
Scenario: A table artifact diffs by rows/cells
  Given a "table" artifact whose version 2 adds a row and edits a cell
  When I diff version 1 against version 2
  Then the added row and the changed cell are reported

@unit
Scenario: A diagram artifact diffs its Mermaid source as text
  Given a "diagram" artifact whose version 2 changes one node label
  When I diff version 1 against version 2
  Then the changed source line is reported
```

### Empty / edge states

```gherkin
@ui
Scenario: Diff mode is unavailable with a single version
  Given an artifact with only version 1
  When I open the artifact editor
  Then diff mode is offered as disabled with a hint that two versions are required
```

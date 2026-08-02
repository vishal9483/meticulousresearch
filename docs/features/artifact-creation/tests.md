# Tests — Artifact Creation

**SPEC:** §3.4 (artifacts & research deliverables), §5 (Artifact/ArtifactVersion), §7.4 (artifact tools). **Milestone:** M3.
**Depends on:** conversations

## Traceability
- §3.4 types (doc/text/code/table/diagram Mermaid) → Types scenarios.
- §3.4 creation path 1 (promote a turn) → Promote scenarios.
- §3.4 creation path 2 (generate directly) → Generate-direct scenarios.
- §3.4 creation path 3 (generate from template) → covered by `deliverable-templates`; this feature exposes the generation seam it calls.
- §3.4 creation path 4 (create blank & edit) → Blank scenarios.
- §5 Artifact/ArtifactVersion schema (current_version_id, version_no, content_format, model, prompt, resource_scope_json, created_by, usage) → Persistence scenarios.
- §7.4 `emit_artifact` / `update_artifact` structured contract → Emit-contract scenarios.
- §9.1(5) generate a Market Research Report from a template → foundation here (the artifact domain + generation seam); template path itself in `deliverable-templates`.

---

```gherkin
Feature: Artifacts
  As an analyst
  I want to create standalone, editable research outputs
  So that I can curate, version, and later export publication-quality deliverables
```

Background common to AI-generating scenarios (per TESTING-STRATEGY §4): a `FakeChatService`
replaces the live API and replays a scripted token stream, usage numbers, and `emit_artifact`
tool call; `IClock` is injected.

```gherkin
Background:
  Given a project "EV Batteries 2026" with 2 enabled resources
  And AI generation is served by a deterministic FakeChatService
```

### Types

```gherkin
@unit
Scenario Outline: An artifact can be created for each supported type
  Given the project workspace is open
  When I create a "<type>" artifact titled "<title>"
  Then an artifact "<title>" of type "<type>" exists
  And its content_format is "<format>"

  Examples:
    | type    | title            | format   |
    | doc     | Exec Summary     | markdown |
    | text    | Raw Notes        | text     |
    | code    | Sizing Script    | code     |
    | table   | Forecast Table   | csv      |
    | diagram | Value Chain      | mermaid  |

@unit
Scenario: A diagram artifact stores Mermaid source
  Given a "diagram" artifact
  When its content is set to a Mermaid flowchart
  Then the content_format is "mermaid"
  And the stored content is the raw Mermaid source (not a rendered image)

@unit
Scenario: An unknown artifact type is rejected
  When I try to create an artifact of type "slide-deck"
  Then creation fails with a validation error
  And no artifact is created
```

### Creation path 1 — promote an assistant turn

```gherkin
@unit
Scenario: Promoting an assistant turn creates a doc artifact from its content
  Given a conversation with an assistant turn containing a market-sizing summary
  When I promote that turn to an artifact titled "Market Sizing"
  Then an artifact "Market Sizing" of type "doc" exists
  And its first version content equals the turn's content
  And the version's created_by is "claude"
  And the version records the turn's model and in-scope resources

@unit
Scenario: Promoting a turn copies its usage onto the version
  Given an assistant turn with tokens_in 1200 and tokens_out 800
  When I promote that turn to an artifact
  Then the created version records tokens_in 1200 and tokens_out 800

@ui
Scenario: Promote-to-artifact is offered on an assistant turn
  Given a conversation with an assistant turn
  When I open the turn's actions
  Then a "Promote to artifact" action is available
```

### Creation path 2 — generate directly

```gherkin
@unit
Scenario: Generating an artifact directly from a prompt and model
  Given the "New artifact" flow is open
  When I enter the prompt "Draft a competitive landscape overview"
  And I select model "claude-opus-5" and include both resources
  And I generate
  Then an artifact is created from the FakeChatService's emitted content
  And its first version records the prompt, model "claude-opus-5", and the 2 in-scope resource ids
  And created_by is "claude"

@unit
Scenario: Direct generation records usage and cost tokens on the version
  Given a FakeChatService scripted to return tokens_in 2000 and tokens_out 1500
  When I generate an artifact directly
  Then the version records tokens_in 2000 and tokens_out 1500

@unit
Scenario: Direct generation requires a non-empty prompt
  Given the "New artifact" flow is open
  When I generate with an empty prompt
  Then I see a validation error
  And no artifact is created

@ui
Scenario: New artifact opens the artifact editor on success
  Given the "New artifact" flow is open
  When I generate an artifact directly
  Then the artifact editor for the new artifact is shown
```

### Creation path 4 — create blank & edit

```gherkin
@unit
Scenario: Creating a blank artifact yields an empty first version
  When I create a blank "doc" artifact titled "Scratch Draft"
  Then an artifact "Scratch Draft" exists
  And it has one version with empty content
  And that version's created_by is "user"

@unit
Scenario: Editing a blank artifact's content persists it
  Given a blank "doc" artifact
  When I set its content to "# Draft" and save
  Then the current version content is "# Draft"
```

### emit_artifact / update_artifact contract (§7.4)

```gherkin
@unit
Scenario: An emit_artifact tool call produces an artifact via the artifact service
  Given the model returns an emit_artifact call with type "table", title "Segment Sizes", and CSV content
  When the generation completes
  Then an artifact "Segment Sizes" of type "table" exists
  And its content was written through the artifact service (not a silent file overwrite)

@unit
Scenario: emit_artifact with a missing required field is rejected
  Given the model returns an emit_artifact call with no title
  When the generation completes
  Then the call is rejected with a contract error
  And no artifact is created
```

### Persistence & schema (§5)

```gherkin
@unit @integration
Scenario: A created artifact and its version match the schema
  When I create an artifact with one version
  Then an Artifact row has id, project_id, title, type, current_version_id, created_at, updated_at
  And an ArtifactVersion row has version_no 1, content, content_format, model, prompt, resource_scope_json, created_by, created_at

@unit
Scenario: current_version_id points at the artifact's only version on creation
  When I create an artifact
  Then the Artifact's current_version_id equals its version_no 1 version id

@unit
Scenario: Artifact content is full-text searchable
  Given an artifact whose version content mentions "lithium iron phosphate"
  When I search the project for "lithium iron phosphate"
  Then the artifact appears in the results
```

### Management basics

```gherkin
@unit
Scenario: Renaming an artifact updates its title and timestamp
  Given an artifact "Draft A"
  When I rename it to "Draft B"
  Then the artifact is named "Draft B"
  And its updated_at is newer than before

@ui
Scenario: The Artifacts view shows a designed empty state
  Given a project with no artifacts
  When I open the project's Artifacts view
  Then I see an empty state with a "New artifact" call to action
```

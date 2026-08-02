# Tests — Artifact Versioning

**SPEC:** §3.4 (versioning + management), §5 (ArtifactVersion). **Milestone:** M3.
**Depends on:** artifact-creation

## Traceability
- §3.4 every edit/regeneration = a new immutable version → Immutability scenarios.
- §3.4 ordered version history → History scenarios.
- §3.4 each version records model/prompt/in-scope resources/timestamp (+ usage/cost §5) → Version-metadata scenarios.
- §3.4 management: set current version → Set-current scenarios.
- §3.4 management: revert to a version → Revert scenarios.
- §3.4 management: duplicate → Duplicate scenarios.
- §3.4 management: delete → Delete scenarios.
- §3.4 management: promote-to-resource → Promote-to-resource scenarios.
- §9.1(5) compare versions → history is the substrate `artifact-diff` compares.

---

```gherkin
Feature: Artifact versioning
  As an analyst
  I want every edit and regeneration to create an immutable version I can navigate and revert
  So that nothing is ever lost and every deliverable's history is auditable (SPEC §1.3)
```

Background (per TESTING-STRATEGY §4): `IClock` is injected so version ordering and timestamps are
deterministic; AI-generating edits are served by a `FakeChatService`.

```gherkin
Background:
  Given an artifact "Market Sizing" with version 1
```

### New version on every change (immutability)

```gherkin
@unit
Scenario: A manual edit creates a new version and leaves the prior one unchanged
  Given the current version 1 content is "# v1"
  When I edit the content to "# v2" and save
  Then a version 2 exists with content "# v2"
  And version 1's content is still "# v1"
  And version 2's created_by is "user"

@unit
Scenario: A regeneration creates a new version
  Given a FakeChatService that emits updated content
  When I regenerate the artifact
  Then a version 2 exists whose created_by is "claude"
  And version 1 is unchanged

@unit
Scenario: A saved version cannot be mutated in place
  Given version 1 exists
  When I attempt to overwrite version 1's content directly
  Then the operation is rejected
  And any change must go through creating a new version
```

### Ordered history

```gherkin
@unit
Scenario: Version numbers increase monotonically
  When I create three successive versions
  Then their version_no values are 1, 2, 3 in creation order

@unit
Scenario: History is ordered newest-to-oldest for display
  Given versions 1, 2, and 3
  When I view the version history
  Then it lists version 3, then 2, then 1
  And each entry shows its created_at, model, and created_by

@ui
Scenario: The version history rail shows all versions
  Given an artifact with 3 versions
  When I open the artifact editor
  Then the version history rail lists 3 versions with the current one marked
```

### Version metadata (§5)

```gherkin
@unit
Scenario: A generated version records full provenance
  Given a FakeChatService returning tokens_in 900 and tokens_out 600
  When I regenerate with model "claude-opus-5" and 2 in-scope resources
  Then the new version records model "claude-opus-5", the prompt, the 2 resource ids, a timestamp, tokens_in 900, tokens_out 600, and a cost_usd

@unit
Scenario: A manual-edit version records zero usage
  When I make a manual edit
  Then the new version's tokens_in, tokens_out, and cost_usd are 0
  And its model and prompt are null
```

### Set current version

```gherkin
@unit
Scenario: Setting an older version as current changes what the editor shows
  Given versions 1, 2, and 3 with version 3 current
  When I set version 1 as current
  Then the artifact's current_version_id points at version 1
  And the editor shows version 1's content
  And versions 2 and 3 still exist in history

@unit
Scenario: Setting current does not create a new version
  Given 3 versions
  When I set version 1 as current
  Then there are still 3 versions
```

### Revert

```gherkin
@unit
Scenario: Reverting to a version creates a new version copying its content
  Given versions 1 ("# v1"), 2 ("# v2"), and 3 ("# v3") with version 3 current
  When I revert to version 1
  Then a new version 4 exists with content "# v1"
  And version 4 is current
  And versions 1–3 are unchanged

@unit
Scenario: A reverted version records that it came from a revert
  When I revert to version 1
  Then the new version's created_by is "user"
```

### Duplicate

```gherkin
@unit
Scenario: Duplicating an artifact copies its full version history
  Given an artifact with 3 versions
  When I duplicate it as "Market Sizing (copy)"
  Then a new artifact "Market Sizing (copy)" exists with 3 versions
  And its current version matches the source's current version content
  And the original artifact is unchanged

@unit
Scenario: A duplicated artifact is independent of the original
  Given a duplicated artifact
  When I edit the duplicate
  Then the original artifact's versions are unaffected
```

### Delete

```gherkin
@unit
Scenario: Deleting an artifact removes it and all its versions
  Given an artifact with 3 versions
  When I delete it and confirm
  Then the artifact no longer exists
  And none of its versions remain

@unit
Scenario: Deleting a single non-current version keeps the rest
  Given versions 1, 2, and 3 with version 3 current
  When I delete version 1
  Then versions 2 and 3 remain
  And version 3 is still current

@unit
Scenario: The current version cannot be deleted directly
  Given version 3 is current
  When I try to delete version 3
  Then the operation is rejected with a message to set another version current first

@ui
Scenario: Deleting an artifact asks for confirmation
  Given an artifact
  When I choose Delete
  Then I am asked to confirm before anything is deleted
```

### Promote-to-resource

```gherkin
@unit
Scenario: Promoting an artifact to a resource creates an artifact_ref resource
  Given an artifact "Forecast Table" whose current version has content
  When I promote it to a resource in the same project
  Then a resource of type "artifact_ref" referencing that artifact exists
  And its extracted text is the artifact's current version content

@unit
Scenario: A promoted resource can be enabled for grounding
  Given an artifact promoted to a resource
  When I enable that resource
  Then it is available as in-scope context for conversations and generations
```

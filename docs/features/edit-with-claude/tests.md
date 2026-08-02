# Tests — Edit with Claude

**SPEC:** §3.4 (editing: manual edit or "Edit with Claude", each creates a version). **Milestone:** M3.
**Depends on:** artifact-versioning, ai-gateway

## Traceability
- §3.4 "Edit with Claude" — a follow-up instruction creates a new version via Claude → Edit-with-Claude scenarios.
- §3.4 manual edit also creates a version → Manual-edit scenarios.
- §3.4 each version records model/prompt/in-scope resources/timestamp (usage §5) → Provenance scenarios.
- §9.1(5) iterate a Market Research Report with "Edit with Claude" → Iterate scenarios.

---

```gherkin
Feature: Edit with Claude
  As an analyst
  I want to refine an artifact by giving Claude a follow-up instruction
  So that I can iterate toward a finished deliverable, with every step versioned
```

Background (per TESTING-STRATEGY §4): AI generation is served by a deterministic
`FakeChatService`; `IClock` is injected. "Edit with Claude" and manual edit both create versions
through the versioning path owned by `artifact-versioning`.

```gherkin
Background:
  Given an artifact "Market Research Report" with version 1 generated from a template
  And AI generation is served by a deterministic FakeChatService
```

### Edit with Claude — follow-up instruction

```gherkin
@unit
Scenario: A follow-up instruction creates a new Claude-authored version
  Given version 1 exists
  When I ask Claude to "Add a 2031 forecast row and tighten the summary"
  And the FakeChatService returns the revised content
  Then a version 2 exists with the revised content
  And version 2's created_by is "claude"
  And version 1 is unchanged

@unit
Scenario: The follow-up sees the current version as context
  Given version 2 is current
  When I ask Claude to "make the tone more formal"
  Then the request sent to the model includes version 2's content as the artifact being edited
  And includes my instruction "make the tone more formal"

@unit
Scenario: The edit is grounded in the project's enabled resources
  Given a project with 2 enabled resources and 1 disabled
  When I ask Claude to "cite sources for the market size claim"
  Then the request's in-scope resources are the 2 enabled resources
  And the new version's resource_scope_json records those 2 ids

@unit
Scenario: A follow-up instruction is required
  When I trigger "Edit with Claude" with an empty instruction
  Then I see a validation error
  And no new version is created
```

### Provenance & usage on a Claude edit (§5)

```gherkin
@unit
Scenario: A Claude edit records model, prompt, and usage
  Given a FakeChatService returning tokens_in 1100 and tokens_out 700
  When I edit with Claude using model "claude-opus-5"
  Then the new version records model "claude-opus-5", the instruction as its prompt, tokens_in 1100, tokens_out 700, and a cost_usd

@unit
Scenario: The model can be chosen per edit
  When I edit with Claude and select model "claude-sonnet-5"
  Then the new version records model "claude-sonnet-5"
```

### Manual edit also creates a version

```gherkin
@unit
Scenario: A manual edit creates a user-authored version
  Given version 1 content "# Draft"
  When I manually change the content to "# Final" and save
  Then a version 2 exists with content "# Final"
  And version 2's created_by is "user"
  And version 2's usage and cost are 0

@unit
Scenario: Saving a manual edit with no changes does not create a version
  Given version 1 content "# Draft"
  When I open the editor and save without changing anything
  Then no new version is created
```

### Streaming, cancel, and failure (via FakeChatService)

```gherkin
@unit
Scenario: An Edit-with-Claude generation streams into a preview before committing
  When I ask Claude to revise the artifact
  Then the revised content streams into a preview
  And a new version is committed only when the stream completes

@unit
Scenario: Cancelling an in-progress Claude edit creates no version
  Given a streaming Claude edit is in progress
  When I cancel it
  Then no new version is created
  And the current version is unchanged

@unit
Scenario: A failed Claude edit surfaces an error and creates no version
  Given the FakeChatService is scripted to return an error
  When I ask Claude to revise the artifact
  Then I see an actionable error message
  And no new version is created
```

### Iterate the flagship report (§9.1(5))

```gherkin
@unit
Scenario: Iterating a Market Research Report produces an ordered version chain
  Given a Market Research Report artifact at version 1
  When I edit with Claude twice with successive instructions
  Then versions 2 and 3 exist in order
  And I can diff version 1 against version 3 to see the cumulative change

@ui
Scenario: The artifact editor exposes an "Edit with Claude" prompt bar
  Given the artifact editor is open
  Then an "Edit with Claude" instruction bar is available with a model selector
```

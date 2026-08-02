# Tests — Model Selector

**SPEC:** §6 (model selection), §6.1 (tiers), §6.2 (additional), §6.3 (config-driven catalog), §3.3 (per-conversation/per-message override). **Milestone:** M2.
**Depends on:** conversations

## Traceability
- §6.1 friendly tiers → concrete model IDs → Tier mapping scenarios.
- §6.2 "All models" additional (non-tier) models → Additional-models scenario.
- §6.3 config-driven catalog JSON with prices (owned here) → Catalog loading scenarios.
- §6 default project model `claude-opus-5` → Default scenario.
- §3.3 model selectable per-conversation and overridable per-message; recorded per turn → Selection/override + Recording scenarios.
- §3.2.1 vision flag; warn/switch if selected model lacks vision → Vision-capability scenario.
- §9.1(4) model selection during a streaming conversation → Selection scenarios underpin it.

> This feature **owns the model catalog JSON** (§6.3) consumed by `ai-gateway` and
> `cost-tracking`. Tests pin the catalog schema, tier→ID mapping, and selection/override.

---

```gherkin
Feature: Model selection
  As an analyst
  I want to pick a model by a friendly tier per conversation or per message
  So that I can trade speed for depth and see which model produced each answer
```

### Config-driven catalog (§6.3)

```gherkin
@unit
Scenario: The default catalog loads the shipped tiers
  Given the app with its default model catalog
  When I read the available tiers
  Then the tiers are "Frontier", "Deep", "Balanced", and "Fast"
  And each tier maps to a concrete model id, context window, max output, and prices

@unit
Scenario Outline: Each default tier maps to the specified model id
  Given the default model catalog
  When I resolve the "<tier>" tier
  Then the model id is "<id>"

  Examples:
    | tier     | id                |
    | Frontier | claude-fable-5    |
    | Deep     | claude-opus-5     |
    | Balanced | claude-sonnet-5   |
    | Fast     | claude-haiku-4-5  |

@unit
Scenario: Additional (non-tier) models are available in the "All models" list
  Given the default model catalog
  When I read the "All models" list
  Then it includes "claude-opus-4-8", "claude-opus-4-7", "claude-sonnet-4-6", and "claude-sonnet-4-5"

@unit
Scenario: The catalog is overridable without a rebuild
  Given a custom catalog JSON that adds a model "claude-mythos-5"
  When the app loads the catalog
  Then "claude-mythos-5" is selectable
  And its prices come from the JSON

@unit
Scenario: A malformed catalog JSON falls back to the shipped default with a clear warning
  Given a catalog file that is not valid JSON
  When the app loads the catalog
  Then the shipped default catalog is used
  And a human-readable warning is surfaced (no stack trace)

@unit
Scenario: The default project model is Claude Opus 5
  Given the default model catalog
  When I read the default model
  Then it is "claude-opus-5"
```

### Selection & override (§3.3)

```gherkin
@unit
Scenario: A conversation inherits the project default model
  Given a project whose default model is "claude-opus-5"
  When I start a new conversation
  Then the conversation's selected model is "claude-opus-5"

@unit
Scenario: Changing the conversation model applies to subsequent turns
  Given a conversation using "claude-opus-5"
  When I change the conversation model to "claude-sonnet-5"
  Then the next turn is sent with model "claude-sonnet-5"

@unit
Scenario: A per-message override does not change the conversation default
  Given a conversation using "claude-sonnet-5"
  When I send one message overridden to "claude-haiku-4-5"
  Then that turn uses "claude-haiku-4-5"
  And the conversation default remains "claude-sonnet-5"

@unit
Scenario: The model used is recorded on the assistant turn
  Given a conversation using "claude-sonnet-5"
  When a turn completes
  Then the assistant message records model "claude-sonnet-5"
```

### Vision capability (§3.2.1)

```gherkin
@unit
Scenario: Selecting a non-vision model with an image in scope warns and offers to switch
  Given a catalog entry "legacy-text-only" with vision=false
  And an image is attached to the turn
  When I select "legacy-text-only"
  Then I see a warning that the model cannot read images
  And I am offered to switch to a vision-capable model
```

### UI

```gherkin
@ui
Scenario: The model picker shows friendly tiers with an "All models" expander
  Given a conversation is open
  When I open the model picker
  Then I see the tiers "Frontier", "Deep", "Balanced", "Fast"
  And an "All models" section listing the additional models

@ui
Scenario: The assistant turn displays the model that produced it
  Given a completed assistant turn produced by "claude-sonnet-5"
  Then the turn shows the model label for "claude-sonnet-5"
```

# Tests — Context Budget

**SPEC:** §3.2 (selection model — budget, warn, help deselect; no silent truncation), §8 (context-budget management: before-send estimate vs model context window + configured budget). **Milestone:** M1.
**Depends on:** token-estimation

## Traceability
- §3.2 selection model — enabled resources up to a configurable context budget; toggle scope → Budget & scope scenarios.
- §3.2 — when resources exceed the budget, warn and (v1) let the user deselect → Warning & deselect scenarios.
- §8 — before send, estimate token usage against the model's context window and the configured budget → Estimate scenarios.
- §8 — **no silent truncation** → No-truncation scenarios.
- §6 / §6.3 — model context window comes from the config-driven catalog → Model-window scenarios.
- §9.1(3)/(4) — resources token-estimated; grounded conversation within budget → covered here for the pre-send check.

---

```gherkin
Feature: Context budget before send
  As an analyst
  I want a before-send estimate against the model window and my budget
  So that I never silently overflow context and can choose what to include
```

```gherkin
Background:
  Given a project with enabled resources whose token estimates sum to a known total
  And a selected model whose context window comes from the model catalog
  And a configured context budget
```

### Before-send estimate

```gherkin
@unit
Scenario: The pre-send estimate sums enabled resources plus fixed overhead
  Given enabled resources estimated at 1,000 and 2,000 tokens
  And custom instructions and message overhead estimated at 500 tokens
  When the before-send estimate is computed
  Then the estimated total is 3,500 tokens
  And it is labeled "estimated"

@unit
Scenario: Disabled resources are excluded from the estimate
  Given enabled resources totaling 3,000 tokens and a disabled resource of 5,000 tokens
  When the before-send estimate is computed
  Then the disabled resource is not counted

@unit
Scenario: Image resources contribute their estimated image tokens
  Given an enabled image resource contributing an estimated image-token amount
  When the before-send estimate is computed
  Then the image tokens are included in the total
```

### Model window & budget checks

```gherkin
@unit
Scenario: The estimate is checked against the selected model's context window
  Given a model with a 200,000-token context window
  And an estimated total of 150,000 tokens
  When the budget check runs
  Then the estimate is within the model window
  And no warning is shown

@unit
Scenario Outline: Exceeding the window or the configured budget warns
  Given a model window of <window> tokens and a configured budget of <budget> tokens
  And an estimated total of <total> tokens
  When the budget check runs
  Then a warning "<warn>" is shown

  Examples:
    | window  | budget  | total   | warn                          |
    | 200000  | 100000  | 90000   | none                          |
    | 200000  | 100000  | 120000  | over configured budget        |
    | 200000  | 250000  | 210000  | over model context window     |
    | 200000  | 100000  | 260000  | over model context window     |

@unit
Scenario: Switching to a larger-window model clears an over-window warning
  Given an estimate that exceeds a 200,000-token model window
  When I switch to a 1,000,000-token model
  Then the over-window warning clears
```

### Help deselect — no silent truncation

```gherkin
@unit
Scenario: When over budget, the app helps the user deselect resources
  Given an estimated total that exceeds the budget
  When I view the budget warning
  Then I am shown which resources contribute most
  And I can deselect resources to bring the total under budget

@unit
Scenario: Deselecting resources recomputes the estimate live
  Given an over-budget estimate
  When I disable the largest resource
  Then the estimated total decreases by that resource's estimate
  And the warning clears once the total is under budget

@unit
Scenario: Content is never silently truncated to fit
  Given an estimated total that exceeds the model window
  When a generation is attempted without resolving the overage
  Then the app does not drop or truncate resources automatically
  And it requires the user to deselect (or switch model) first
```

### UI

```gherkin
@ui
Scenario: The composer shows a live budget meter and warning
  Given a conversation with resources in scope
  Then I see an estimated-tokens meter against the model window and budget
  And it turns to a warning state when the estimate exceeds the budget

@ui
Scenario: The warning offers deselect and switch-model actions
  Given the budget is exceeded
  When I open the budget warning
  Then I can deselect resources or switch to a larger-window model from there
```

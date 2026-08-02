# Tests — Cost Tracking & Usage Metering

**SPEC:** §3.6 (cost tracking & usage metering), §6.3 (config-driven price catalog). **Milestone:** M4.
**Depends on:** turn-metadata-actions (tokens persisted per turn), model-selector (model catalog + prices)

## Traceability
- §3.6 per-turn tokens in/out + computed cost badge → Per-turn cost scenarios.
- §3.6 per-conversation running total → Per-conversation scenarios.
- §3.6 per-project consolidated (conversations vs artifacts, by model, by time window) → Consolidated scenarios.
- §3.6 cost = tokens × config prices incl cache-read/cache-write → Cost computation scenarios.
- §3.6 tokens are ground truth; cost computed from current prices so a price update reprices history → Repricing scenarios.
- §3.6 token counts from API usage fields are authoritative (not local estimation) → Provenance scenario.
- §6.3 prices are USD per MTok → Cost computation scenarios.
- §9.1(7) consolidated project cost → Consolidated scenarios.

> **Ownership note:** This feature **owns cost computation** for the whole app. It reads the
> per-model prices from the model catalog (owned by `model-selector`, §6.3) and the persisted
> token counts on `Message` / `ArtifactVersion` (owned by `turn-metadata-actions` / artifacts).
> All cost tests are `@unit` with **fixed token inputs**, a **fixed price table**, and an
> **injected `IClock`** for time windows (TESTING-STRATEGY §4).

---

```gherkin
Feature: Cost tracking & usage metering
  As an analyst
  I want to see what each turn, conversation, and project costs
  So that I can report and control spend on a piece of research
```

## Background

```gherkin
Background:
  Given a fixed price table in USD per million tokens:
    | model           | input | output | cache_read | cache_write |
    | claude-opus-5   | 5     | 25     | 0.5        | 6.25        |
    | claude-sonnet-5 | 3     | 15     | 0.3        | 3.75        |
    | claude-haiku-4-5| 1     | 5      | 0.1        | 1.25        |
  And an injected clock
```

### Cost computation (the core formula)

```gherkin
@unit
Scenario: Cost of a turn is tokens times per-MTok prices
  Given a turn on "claude-opus-5" with 1000000 input tokens and 200000 output tokens
  When its cost is computed
  Then the cost is 5.00 USD for input and 5.00 USD for output
  And the total cost is 10.00 USD

@unit
Scenario Outline: Cost is computed per model from the price table
  Given a turn on "<model>" with <in> input tokens and <out> output tokens
  When its cost is computed
  Then the total cost is <cost> USD
  Examples:
    | model            | in      | out    | cost  |
    | claude-opus-5    | 500000  | 100000 | 5.00  |
    | claude-sonnet-5  | 1000000 | 100000 | 4.50  |
    | claude-haiku-4-5 | 2000000 | 200000 | 3.00  |
    | claude-opus-5    | 0       | 0      | 0.00  |

@unit
Scenario: Cache-read and cache-write tokens are priced at their own rates
  Given a turn on "claude-opus-5" with:
    | input | output | cache_read | cache_write |
    | 100000| 50000  | 200000     | 40000       |
  When its cost is computed
  Then the input component is 0.50 USD
  And the output component is 1.25 USD
  And the cache-read component is 0.10 USD
  And the cache-write component is 0.25 USD
  And the total cost is 2.10 USD

@unit
Scenario: Fractional-cent precision is retained, not truncated to whole cents
  Given a turn on "claude-haiku-4-5" with 1234 input tokens and 567 output tokens
  When its cost is computed
  Then the total cost equals 1234/1000000*1 + 567/1000000*5 USD
  And the stored value keeps at least 6 decimal places of a dollar

@unit
Scenario: An unknown model has no price and is reported, not silently zero
  Given a turn on "claude-mythos-5" which is absent from the price table
  When its cost is computed
  Then the cost is flagged as unknown-price
  And the turn is excluded from priced totals rather than counted as 0.00
```

### Per-turn cost badge

```gherkin
@unit
Scenario: A completed turn exposes tokens and computed cost for its badge
  Given a completed assistant turn on "claude-sonnet-5" with 300000 input and 60000 output tokens
  When I view the turn
  Then the turn shows input tokens 300000, output tokens 60000
  And the turn shows a computed cost of 1.80 USD

@ui
Scenario: The per-turn cost badge is shown inline with a hover breakdown
  Given a conversation with one completed assistant turn
  When I view the turn
  Then a cost badge is shown inline on the turn
  And hovering it shows a breakdown of input, output, and cache token costs
```

### Per-conversation running total

```gherkin
@unit
Scenario: Conversation total is the sum of its turns' costs
  Given a conversation with turns costing 1.80, 0.90, and 2.30 USD
  When I read the conversation total
  Then the total cost is 5.00 USD
  And the total tokens equal the sum of the turns' tokens

@unit
Scenario: A conversation with no completed turns has a zero total
  Given a conversation with no assistant turns
  When I read the conversation total
  Then the total cost is 0.00 USD

@ui
Scenario: The conversation header updates its running cost as a turn completes
  Given a conversation with a running cost of 5.00 USD
  When a new turn completes costing 1.50 USD
  Then the conversation header shows a running cost of 6.50 USD
```

### Per-project consolidated cost

```gherkin
@unit
Scenario: Consolidated project cost sums conversations and artifact generations
  Given a project with conversation turns costing 5.00 USD total
  And artifact-version generations costing 3.00 USD total
  When I read the consolidated project cost
  Then the total cost is 8.00 USD

@unit
Scenario: Consolidated cost breaks down by conversations vs artifacts
  Given a project with 5.00 USD of conversation cost and 3.00 USD of artifact-generation cost
  When I read the consolidated breakdown by source
  Then the conversations bucket is 5.00 USD
  And the artifacts bucket is 3.00 USD

@unit
Scenario: Consolidated cost breaks down by model
  Given a project with:
    | model           | cost |
    | claude-opus-5   | 6.00 |
    | claude-sonnet-5 | 1.50 |
    | claude-haiku-4-5| 0.50 |
  When I read the consolidated breakdown by model
  Then each model tier reports its own spend
  And the model buckets sum to the project total 8.00 USD

@unit
Scenario Outline: Consolidated cost breaks down by time window using the injected clock
  Given the clock is set to "2026-08-03T12:00:00"
  And priced usage at:
    | when                | cost |
    | 2026-08-03T09:00:00 | 2.00 |
    | 2026-07-30T09:00:00 | 3.00 |
    | 2026-06-01T09:00:00 | 5.00 |
  When I read the consolidated cost for the "<window>" window
  Then the window total is <total> USD
  Examples:
    | window   | total |
    | today    | 2.00  |
    | week     | 5.00  |
    | all-time | 10.00 |

@ui
Scenario: The project dashboard shows the consolidated cost panel with breakdowns
  Given a project with recorded usage
  When I open the project dashboard
  Then a consolidated cost panel shows total spend
  And it shows breakdowns by conversations-vs-artifacts, by model, and by time window
```

### Tokens are ground truth — a price update reprices history

```gherkin
@unit
Scenario: Updating a price reprices historical usage from stored tokens
  Given a project with a turn on "claude-opus-5" of 1000000 input and 0 output tokens
  And the consolidated cost currently reads 5.00 USD
  When the price table for "claude-opus-5" input changes to 10 per MTok
  And I read the consolidated project cost again
  Then the turn's cost is now 10.00 USD
  And no stored token counts changed

@unit
Scenario: The snapshot cost stored on a turn does not change the recomputed total
  Given a turn whose snapshot cost_usd was recorded as 5.00 at completion time
  And the current price table would compute 10.00 for it
  When the consolidated cost is computed from current prices
  Then the total uses the current-price value 10.00
  And the historical snapshot 5.00 remains available for audit
```

### Provenance — token counts are authoritative from the API

```gherkin
@unit
Scenario: Token counts come from API usage fields, not local estimation
  Given an assistant turn whose API response reported usage of 300000 input and 60000 output tokens
  When the turn is persisted
  Then the stored tokens_in is 300000 and tokens_out is 60000
  And the stored counts are marked authoritative, not estimated

@unit
Scenario: Pre-send local estimates are never mixed into cost totals
  Given a pending message with a local pre-send estimate of 250000 tokens
  When consolidated cost is computed
  Then the pending estimate is excluded from priced totals
  And only completed turns with authoritative usage are counted
```

### Optional budget guardrail (config, off by default)

```gherkin
@unit
Scenario: A soft monthly budget shows a warning when exceeded and never blocks
  Given a project with a soft monthly budget of 10.00 USD enabled
  And this month's consolidated cost is 8.00 USD
  When a new turn completes costing 3.00 USD
  Then a budget-exceeded warning is raised
  And the turn is still recorded and not blocked

@unit
Scenario: The budget guardrail is off by default
  Given a new project with no budget configured
  When this month's consolidated cost reaches 100.00 USD
  Then no budget warning is raised
```

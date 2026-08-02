# Tests — Turn Metadata & Actions

**SPEC:** §3.3 (turn metadata + actions), §3.6 (per-turn cost badge), §5 (Message fields). **Milestone:** M2.
**Depends on:** streaming

## Traceability
- §3.3 per-turn metadata: model, token usage (in/out), latency, resource scope → Metadata scenarios.
- §3.3 actions: copy, retry (same/other model), edit-and-resend, promote to artifact, delete → Action scenarios.
- §3.6 per-turn cost badge (tokens + computed cost; full breakdown on expand) → Cost badge scenarios.
- §9.1(4) per-turn cost during a streaming conversation → Metadata + Cost badge scenarios.

> Cost computation itself is owned by `cost-tracking` (M4); here the per-turn badge consumes it
> (mocked/priced from the catalog). Uses `FakeChatService`; no network.

---

```gherkin
Feature: Turn metadata and actions
  As an analyst
  I want each assistant turn to show its metadata and offer actions
  So that I can see what an answer cost and act on it
```

### Turn metadata (§3.3)

```gherkin
@unit
Scenario: A completed turn exposes model, token usage, latency, and resource scope
  Given a turn produced by "claude-sonnet-5" with usage in=900 out=200
  And resources "A" and "B" were in scope
  When I inspect the turn's metadata
  Then it shows model "claude-sonnet-5"
  And input tokens 900 and output tokens 200
  And a latency value
  And resource scope "A", "B"

@ui
Scenario: Turn metadata is visible without leaving the thread
  Given a completed assistant turn
  When I expand the turn's details
  Then I see its model, token usage, latency, and which resources were in scope
```

### Per-turn cost badge (§3.6)

```gherkin
@unit
Scenario: The per-turn badge shows a computed cost from tokens and catalog prices
  Given "claude-sonnet-5" priced at input $3/MTok and output $15/MTok
  And a turn with input tokens 1,000,000 and output tokens 1,000,000
  When the turn completes
  Then the per-turn cost is $18.00

@unit
Scenario: Cache tokens are included in the per-turn cost breakdown
  Given a turn reporting cache_read and cache_write tokens
  When I expand the cost breakdown
  Then it itemizes input, output, cache-read, and cache-write contributions

@ui
Scenario: The cost badge is inline with a full breakdown on hover/expand
  Given a completed assistant turn
  Then a small cost badge is shown inline
  And expanding it reveals the full token/cost breakdown
```

### Actions (§3.3)

```gherkin
@unit
Scenario: Copy places the assistant turn's text on the clipboard
  Given a completed assistant turn with text "The TAM is $12B"
  When I copy the turn
  Then the clipboard contains "The TAM is $12B"

@unit
Scenario: Retry (same model) generates a fresh answer to the same question
  Given a user question and its assistant answer using "claude-opus-5"
  When I retry with the same model
  Then a new assistant turn is generated for the same question using "claude-opus-5"

@unit
Scenario: Retry with another model uses the chosen model
  Given an assistant answer produced by "claude-opus-5"
  When I retry with "claude-haiku-4-5"
  Then a new assistant turn is generated using "claude-haiku-4-5"

@unit
Scenario: Edit-and-resend replaces the user message and regenerates
  Given a user message "What is the CAGR?" with an assistant answer
  When I edit it to "What is the 10-year CAGR?" and resend
  Then the user message becomes "What is the 10-year CAGR?"
  And a new assistant turn is generated for the edited message

@unit
Scenario: Promote to artifact creates an artifact from the assistant turn
  Given a completed assistant turn
  When I promote it to an artifact
  Then an artifact is created carrying the turn's content
  And the source turn, model, and resource scope are recorded as provenance
  # (artifact domain is owned by M3; here we assert the promote request/provenance)

@unit
Scenario: Deleting a turn removes it from the conversation
  Given a conversation with an assistant turn
  When I delete the turn
  Then the turn no longer appears in the conversation

@ui
Scenario: A turn exposes copy, retry, edit-and-resend, promote, and delete actions
  Given a completed assistant turn
  When I open its action menu
  Then I see "Copy", "Retry", "Edit & resend", "Promote to artifact", and "Delete"
```

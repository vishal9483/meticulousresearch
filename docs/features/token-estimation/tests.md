# Tests — Token Estimation

**SPEC:** §3.2 (per-resource token estimate), §3.6 (pre-send estimates are local and labeled "estimated"). **Milestone:** M1.
**Depends on:** text-paste-resource

## Traceability
- §3.2 per-resource token estimate → Estimate scenarios.
- §3.6 provenance — pre-send estimates use local estimation and are **labeled "estimated"** (authoritative counts come from API usage) → Labeling scenarios.
- TESTING-STRATEGY §4 — token estimation must be deterministic for a given input → Determinism scenarios.
- §9.1(3) resources are token-estimated → covered here.

---

```gherkin
Feature: Local token estimation
  As an analyst
  I want a fast local estimate of each resource's token cost
  So that I can plan context before spending on a real model call
```

### Deterministic local estimate

```gherkin
@unit
Scenario: Estimating the same text twice yields the same number
  Given the text "Global foundry capacity grew 12% in 2025."
  When I estimate its tokens twice
  Then both estimates are equal

@unit
Scenario: The estimator runs locally with no network call
  Given any input text
  When I estimate its tokens
  Then no network or model API call is made

@unit
Scenario Outline: Longer inputs estimate to more tokens
  Given input "<text>"
  When I estimate its tokens
  Then the estimate is at least <min>

  Examples:
    | text                                   | min |
    | ok                                     | 1   |
    | Global foundry capacity grew in 2025.  | 5   |

@unit
Scenario: Empty text estimates to zero tokens
  Given empty text
  When I estimate its tokens
  Then the estimate is 0
```

### Per-resource estimates

```gherkin
@unit
Scenario: A resource's token estimate is derived from its extracted text
  Given a text resource with extracted text of a known length
  When its token estimate is computed
  Then token_estimate equals the estimator's result for that text

@unit
Scenario: Image resources contribute an estimated image-token amount
  Given an image resource
  When its token estimate is computed
  Then it contributes a non-zero image-token estimate toward context

@unit
Scenario: Re-extraction recomputes the token estimate
  Given a resource whose extracted text changes on re-extract
  When it is re-extracted
  Then its token_estimate is recomputed from the new text
```

### Labeling & honesty

```gherkin
@unit
Scenario: Estimates are surfaced with an "estimated" label
  Given a resource token estimate
  When it is shown in the UI
  Then it is labeled as "estimated" (not an authoritative count)

@unit
Scenario: The estimator approximates model tokenization within a stated tolerance
  Given a reference text with a known approximate token count
  When I estimate its tokens
  Then the estimate is within the documented tolerance of the reference
```

### UI

```gherkin
@ui
Scenario: The resources table shows an estimated token column
  Given the Resources view lists resources
  Then each row shows an "estimated" token count
```

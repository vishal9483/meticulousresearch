# Tests — Rate-Limit & Transient-Error Backoff

**SPEC:** §8 (429 + transient 5xx → exponential backoff + jitter, honor retry-after; "retrying…" state with attempt count; do not fail/lose work). **Milestone:** M2.
**Depends on:** ai-gateway

## Traceability
- §8 HTTP 429 → automatic exponential backoff + jitter → Backoff scenarios.
- §8 transient 5xx handled the same way → 5xx scenarios.
- §8 honor `retry-after` when present → Retry-after scenarios.
- §8 surface a "retrying…" state with attempt count rather than failing → Retry-state scenarios.
- §8 do not fail the generation / do not lose work → No-loss scenarios.
- §9.1(8) experience a rate-limit event and observe automatic retry/backoff without losing work → this whole feature.

> Uses `FakeChatService` scripted to return 429/5xx sequences (owned by `ai-gateway`) and an
> **injected `IClock`** so backoff delays and jitter are deterministic (TESTING-STRATEGY §4).
> No real network and no real waiting.

---

```gherkin
Feature: Rate-limit and transient-error backoff
  As an analyst
  I want the app to automatically retry when rate-limited or hit a transient error
  So that a long generation survives instead of failing and losing my work
```

### Automatic backoff on 429 / 5xx

```gherkin
@unit
Scenario: A 429 is retried automatically and then succeeds
  Given a backend scripted to return 429 once, then succeed
  When I ask a question
  Then the request is retried automatically
  And the generation ultimately succeeds
  And I never see a hard failure

@unit
Scenario Outline: Transient 5xx errors are retried like 429
  Given a backend scripted to return "<status>" once, then succeed
  When I ask a question
  Then the request is retried automatically and succeeds

  Examples:
    | status |
    | 500    |
    | 502    |
    | 503    |
    | 529    |

@unit
Scenario: Backoff grows exponentially with jitter across attempts
  Given a backend that returns 429 three times, then succeeds
  And a deterministic jitter source
  When I ask a question
  Then the delay before each retry increases roughly exponentially
  And a jitter component is applied to each delay

@unit
Scenario: A non-retryable error (e.g. 401) is not retried
  Given a backend scripted to return 401 Unauthorized
  When I ask a question
  Then the request is not retried
  And I see a clear, actionable error (invalid key)
```

### Honor retry-after (§8)

```gherkin
@unit
Scenario: When retry-after is present, the wait honors it
  Given a backend that returns 429 with retry-after 7 seconds, then succeeds
  When I ask a question
  Then the retry waits at least 7 seconds (per the injected clock)
  And then succeeds

@unit
Scenario: retry-after overrides a shorter computed backoff
  Given a computed backoff of 2 seconds
  And a 429 with retry-after 10 seconds
  Then the wait is 10 seconds, not 2
```

### "Retrying…" state with attempt count (§8)

```gherkin
@unit
Scenario: The UI reflects a "retrying…" state with the current attempt number
  Given a backend that returns 429 twice, then succeeds
  When I ask a question
  Then I see a "retrying…" state on attempt 1
  And a "retrying…" state on attempt 2 showing the attempt count
  And the state clears when the generation succeeds

@ui
Scenario: A rate-limited generation shows a non-alarming retry indicator, not an error
  Given a generation that is being retried after a 429
  Then the thread shows a "retrying…" indicator with the attempt count
  And no error dialog or raw status code is shown
```

### Do not lose work (§8 / §9.1(8))

```gherkin
@unit
Scenario: A retry does not duplicate or discard the user's message
  Given a user message that triggers a 429 then succeeds on retry
  When the generation completes
  Then exactly one user message is persisted
  And exactly one assistant message is persisted

@unit
Scenario: Retries stop after a maximum and preserve partial work
  Given a backend that returns 429 on every attempt up to the retry limit
  When the limit is reached
  Then the generation stops retrying
  And any partial streamed text is persisted and marked interrupted
  And the user is offered to retry manually
  And nothing is lost

@unit
Scenario: A 429 mid-stream resumes without losing already-streamed tokens
  Given a stream that emits "The market " then hits a 429
  When the backoff retry continues the generation
  Then the already-streamed text "The market " is preserved
  And the final answer includes it
```

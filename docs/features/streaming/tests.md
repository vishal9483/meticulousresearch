# Tests — Streaming

**SPEC:** §3.3 (token-by-token streaming, stop/cancel), §8 (interrupted stream persisted/resumable/marked interrupted). **Milestone:** M2.
**Depends on:** conversations

## Traceability
- §3.3 assistant responses stream token-by-token to the UI → Streaming scenarios.
- §3.3 stop/cancel a streaming generation → Stop/cancel scenarios.
- §8 interrupted stream is persisted, resumable, and marked interrupted → Interruption scenarios.
- §9.1(4) hold a grounded, **streaming** conversation → Streaming scenarios underpin the acceptance bar.

> Uses `FakeChatService` (owned by `ai-gateway`) to replay scripted token streams and injected
> interruptions; the injected `IClock` makes any timing deterministic. No network.

---

```gherkin
Feature: Streaming responses
  As an analyst
  I want assistant replies to appear token-by-token and be stoppable
  So that I get fast feedback and never lose partial work
```

### Token-by-token streaming

```gherkin
@unit
Scenario: Tokens are appended to the assistant turn as they arrive
  Given a backend scripted to emit "Mar", "ket ", "size" then complete
  When I ask a question
  Then the assistant turn's text shows "Mar", then "Market ", then "Market size" as tokens arrive
  And the final persisted text is "Market size"

@unit
Scenario: A streaming cursor/indicator is shown while tokens are arriving and clears on completion
  Given a backend that streams then completes
  When the response is streaming
  Then the turn is in a "streaming" state
  And when it completes the "streaming" state clears

@ui
Scenario: The reply renders incrementally in the thread
  Given a conversation is open
  When I send a message and the backend streams a reply
  Then I see text appear progressively rather than all at once
```

### Stop / cancel

```gherkin
@unit
Scenario: Stopping a stream halts token delivery
  Given a response is streaming
  When I stop the generation
  Then no further tokens are appended
  And the turn is no longer in a "streaming" state

@unit
Scenario: A stopped turn persists the partial text and is marked interrupted
  Given a response has streamed "The market is grow" when I stop it
  Then the assistant turn is persisted with text "The market is grow"
  And the turn is marked interrupted
  And no work is lost

@ui
Scenario: Esc / Stop cancels an in-progress generation
  Given a response is streaming
  When I press Stop
  Then streaming ends
  And the partial answer remains visible in the thread
```

### Interruption & resume (§8)

```gherkin
@unit
Scenario: A backend interruption mid-stream persists the partial turn marked interrupted
  Given a backend that emits "Segment A: " then faults with a retryable error
  When I ask a question
  Then the assistant turn is persisted with the partial text "Segment A: "
  And the turn is marked interrupted
  And the failure is surfaced as retryable, not a lost turn

@unit
Scenario: An interrupted turn can be resumed
  Given an interrupted assistant turn with partial text "Segment A: "
  When I resume it and the backend continues with "growing at 8% CAGR"
  Then the turn's text becomes "Segment A: growing at 8% CAGR"
  And the turn is no longer marked interrupted

@unit
Scenario: Completing normally does not mark the turn interrupted
  Given a backend that streams and completes cleanly
  When the turn completes
  Then the turn is not marked interrupted

@ui
Scenario: An interrupted turn offers a "resume"/"retry" affordance
  Given an assistant turn was interrupted mid-stream
  Then the turn shows it was interrupted
  And offers an action to continue the generation
```

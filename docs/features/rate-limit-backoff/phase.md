# Phase — Rate-Limit & Transient-Error Backoff

**SPEC:** §8. **Milestone:** M2. **Depends on:** ai-gateway

## Goal
Make generation dependable under real load (§8): automatically retry HTTP **429** and transient
**5xx** with **exponential backoff + jitter**, **honor `retry-after`**, surface a **"retrying…"
state with attempt count**, and **never fail the turn or lose work**. This directly addresses the
headline pain of analysts being rate-limited mid-document (§9.1(8)).

## Deliverables
1. **Retry policy** — a backoff engine wrapping `IChatService` requests: classifies errors
   (retryable: 429 + transient 5xx incl. 529; non-retryable: 401/400/etc.), computes exponential
   backoff with jitter, caps at a max attempt count, and honors `retry-after` when present
   (taking the max of computed backoff and retry-after).
2. **Retry-state signal** — a `Retrying { attempt, nextDelay }` state exposed to the VM so the UI
   can show a "retrying…" indicator with the attempt number; clears on success or final failure.
3. **Work preservation** — retries are idempotent w.r.t. persistence (exactly one user + one
   assistant message); a 429 mid-stream continues without discarding already-streamed tokens;
   on exhausting retries, persist partial text marked interrupted and offer manual retry.
4. **Injectable timing** — all delays go through `IClock`/a delay abstraction and a deterministic
   jitter source so tests never actually wait.

## Suggested design
- Implement as a decorator over `IChatService` (from `ai-gateway`) so every backend and every
  consumer (conversations, streaming, artifacts) gets backoff for free.
- Error classification lives in `ai-gateway` (it knows sidecar vs. HTTP faults); this feature
  consumes the classification and owns the *policy*.
- Backoff: `delay = base * 2^(attempt-1)`, plus jitter (full or equal jitter); `retry-after`
  (seconds or HTTP-date) overrides when larger. Cap attempts; expose `attempt` in the state.
- Coordinate with `streaming`: mid-stream 429 keeps the accumulated partial and continues;
  exhausted retries hand off to streaming's interrupted-persist path (nothing lost).
- Deterministic tests: inject `IClock` for delays and a seeded/fixed jitter source; drive with
  `FakeChatService` scripted 429/5xx sequences.

## Test-first order
1. Classification + retry-then-succeed `@unit` tests (429, 5xx outline, 401 not retried) → policy.
2. Backoff-growth + jitter `@unit` tests → delay computation with injected clock/jitter.
3. `retry-after` `@unit` tests (honored; overrides shorter backoff) → retry-after handling.
4. Retry-state `@unit` + `@ui` tests (attempt count, non-alarming indicator) → state signal.
5. No-loss `@unit` tests (no duplicate messages; exhaustion persists partial; mid-stream resume)
   → persistence idempotence + streaming handoff.

## Definition of done
- 429 and transient 5xx retry automatically with exponential backoff + jitter and succeed when
  the backend recovers; non-retryable errors are surfaced immediately with actionable messages.
- `retry-after` is honored and overrides a shorter computed delay.
- The UI shows a "retrying…" state with attempt count (no error dialog / raw status).
- No duplicated or lost messages; mid-stream 429 preserves streamed tokens; exhausted retries
  persist partial work marked interrupted and offer manual retry.
- All timing is deterministic under the injected clock (tests do not wait).

## Notes for later features
- `prompt-caching` reduces input cost/latency on retries (cached input is reused) — orthogonal
  but complementary; both wrap the same request path.
- `v1-acceptance` §9.1(8) exercises this end-to-end (a scripted rate-limit event without losing
  work).

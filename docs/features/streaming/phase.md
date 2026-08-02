# Phase — Streaming

**SPEC:** §3.3, §8. **Milestone:** M2. **Depends on:** conversations

## Goal
Render assistant replies token-by-token, let the user stop/cancel a generation, and ensure an
interrupted stream is **persisted, marked interrupted, and resumable** (§8). Builds on the
conversation Ask flow and the `IChatService` token stream from `ai-gateway`.

## Deliverables
1. **Streaming consumption** — the conversation VM subscribes to the `IChatService` token stream
   and appends deltas to the live assistant turn; exposes a `Streaming` state that clears on
   completion, cancellation, or fault.
2. **Stop/cancel** — a stop command cancels the request (via the cancellation token threaded
   through `ai-gateway`); halts token delivery immediately.
3. **Partial persistence** — on stop or interruption, persist the assistant `Message` with the
   accumulated partial text and an **`interrupted`** marker; on clean completion, persist final
   text with `interrupted` false. No path loses the partial work.
4. **Resume** — an interrupted turn can be continued: re-issue generation with the existing
   partial as context and append further tokens, clearing the interrupted marker on completion.
5. **UI** — incremental rendering with a streaming cursor/indicator, a Stop control (Esc per
   §3.5), and an interrupted-turn affordance to resume/retry.

## Suggested design
- Model the assistant turn's state machine: `Streaming → Completed` | `Streaming → Interrupted`
  (via stop or retryable fault) `→ Streaming (resume) → Completed`.
- Persist the partial on both explicit stop and backend fault so §8 "nothing is lost" holds for
  either cause. The retryable-fault signal comes from `ai-gateway` (sidecar crash / transient).
- Resume strategy for M2: re-send with the partial answer included so the model continues; a
  true provider-side resumable-stream token is out of scope. Keep the seam so it can improve.
- Keep append/finalize logic in the VM/Core so it is `@unit`-testable with `FakeChatService`;
  the `@ui` tests only prove incremental rendering and the Stop/resume wiring.
- Use injected `IClock` for any latency/indicator timing.

## Test-first order
1. Token-append + streaming-state `@unit` tests → stream consumption in the VM.
2. Stop/cancel `@unit` tests (halt + partial persisted + marked interrupted) → stop command +
   partial persistence.
3. Interruption/resume `@unit` tests (fault persists partial; resume continues & clears marker;
   clean completion not marked) → fault handling + resume.
4. `@ui` tests (incremental render, Stop, interrupted affordance) → thread view wiring.

## Definition of done
- Tokens append live and the streaming indicator clears on completion.
- Stop halts delivery and persists the partial text marked interrupted; nothing is lost.
- A backend interruption persists the partial (interrupted) and surfaces retryable; resume
  continues the turn and clears the marker; clean completion is never marked interrupted.

## Notes for later features
- `rate-limit-backoff` wraps generation so 429/5xx retry automatically before a turn is treated
  as interrupted; a truly interrupted turn still persists partial + marker.
- `turn-metadata-actions` "retry" reuses the resume/re-generate path.
- `prompt-caching` operates on the request assembly; streaming is unaffected by cache breakpoints.

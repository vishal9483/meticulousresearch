# Phase — AI Gateway (IChatService / IArtifactService)

**SPEC:** §7.2, §7.3, §7.1, §8. **Milestone:** M2. **Depends on:** settings-secure-key

## Goal
Introduce the app's single generation abstraction. Owns the **`IChatService` and
`IArtifactService` contracts** and both backends behind them: the **Agent SDK sidecar
(primary)** over a loopback WebSocket, and a **C# direct Anthropic Messages API (fallback)**.
The rest of the app depends only on the interfaces and is unaware which backend is active.

## Deliverables
1. **`IChatService`** — the contract every consumer uses:
   - `Ask(project, conversation, message, model, resourceScope, cancellationToken)` returning a
     stream of token events plus a terminal completion carrying final text and **usage**
     (input, output, cache_read, cache_write).
   - Cancellation stops the stream promptly and completes in a cancelled state.
   - Request assembly (system = custom instructions, + in-scope resources, + history, + message)
     per §7.3 lives here so both backends send identical payloads.
2. **`IArtifactService`** — the artifact emit/update contract surface (structured
   `emit_artifact` / `update_artifact` results); defined here, exercised by later features
   (`builtin-file-tools-sandbox`, M3 artifacts). Keep it minimal but stable.
3. **`SidecarChatService`** (primary) — launches the Node/TypeScript Agent SDK sidecar,
   connects over a loopback WebSocket (ephemeral port, per-session token), passes the API key
   over the authenticated channel, streams `query()` results back as token events.
4. **`DirectApiChatService`** (fallback) — pure C# streaming Messages API client implementing
   the same contract; no Node, no sidecar. Surfaces the same usage fields.
5. **Backend selection** — resolved from `ISettingsService` (default: sidecar); a
   `IChatBackendFactory` returns the active `IChatService`.
6. **Sidecar supervisor** — launch, health, and **auto-restart on crash** (§8) with throttled
   backoff after repeated immediate failures; re-establishes token + endpoint on restart.
7. **Test doubles** — `FakeChatService : IChatService` (owned here, per TESTING-STRATEGY §4)
   that replays scripted token streams, usage numbers, and error codes (429/5xx) so every
   downstream feature is deterministic and `@unit`-testable.

## Suggested design
- **Contract-first.** Pin `IChatService`/`IArtifactService` and `FakeChatService` before either
  real backend, since every M2 feature and M3 artifact work consumes them.
- Represent the stream as an async sequence of events: `TokenDelta`, then a single
  `Completed { Text, Usage }`, or `Cancelled` / `Faulted(retryable)`.
- Key & endpoint handling: obtain the **effective key and base URL** from
  `IApiCredentialProvider` (settings-secure-key) at request/launch time only — `ANTHROPIC_API_KEY`
  / `ANTHROPIC_BASE_URL` env vars win, then the secure store / persisted setting, then default
  public API. Never place the key on the sidecar command line; deliver it over the authenticated
  WebSocket after the token handshake. The base URL is **never hardcoded**: the `DirectApiChatService`
  targets the resolved base URL, and the sidecar receives it over the same authenticated channel
  (or via the Agent SDK's `ANTHROPIC_BASE_URL` on the sidecar process env) so both backends hit
  the same endpoint.
- Sidecar transport: loopback WebSocket recommended (§7.2) for clean streaming; bind to
  `127.0.0.1`, ephemeral port, per-launch token; refuse unauthenticated connections.
- Supervisor: a crash mid-stream faults the in-flight request as **retryable** (do not silently
  drop it); `rate-limit-backoff` and `streaming` decide how the turn recovers. Repeated crashes
  → throttle + "backend unavailable" surfaced through the same error channel.
- Keep transient-error classification (429/5xx) here so `rate-limit-backoff` can wrap it, but do
  not implement the backoff policy here.

## Test-first order
1. Contract + `FakeChatService` `@unit` tests (token order, completion, usage, cancellation,
   request assembly, backend-equivalence) → interfaces + fake.
2. Usage-capture Scenario Outline (both backends) → shared usage mapping.
3. Backend-selection tests → settings wiring + factory.
4. Sidecar `@unit @integration` transport/security tests → `SidecarChatService` + supervisor.
5. Auto-restart tests → supervisor restart/throttle.
6. Direct-API contract test → `DirectApiChatService`; `@requires-network` acceptance last.

## Definition of done
- `IChatService`/`IArtifactService` and `FakeChatService` exist and are consumed only via the
  interfaces; downstream features never reference a concrete backend.
- Both backends stream tokens, complete, and surface identical usage fields (cache fields
  default to 0 when absent).
- Sidecar binds loopback + ephemeral port with per-session token; key never on the command line
  and sourced via `IApiCredentialProvider` (env wins, else secure store); crashed sidecar
  auto-restarts with throttling.
- Both backends send requests to the **resolved base URL** (`ANTHROPIC_BASE_URL` env → setting →
  default public API); no endpoint is hardcoded, and the same value reaches sidecar and direct-API.
- Missing key and unavailable-backend produce human-readable, actionable errors (no stack traces).

## Notes for later features
- `conversations` calls `Ask(...)` and persists `Message` + usage on completion.
- `streaming` consumes the token stream and owns stop/cancel UX and resumable persistence.
- `model-selector` supplies the model id per turn (it owns the catalog JSON).
- `rate-limit-backoff` wraps `IChatService` to retry the retryable/429/5xx faults classified here.
- `prompt-caching` sets cache breakpoints on the assembled system prompt + stable resources.
- `builtin-file-tools-sandbox` plugs the curated tool set into the sidecar loop and consumes
  `IArtifactService`.

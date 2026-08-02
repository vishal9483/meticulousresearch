# Phase — Cost Tracking & Usage Metering

**SPEC:** §3.6, §6.3. **Milestone:** M4. **Depends on:** turn-metadata-actions, model-selector

## Goal
Own **cost computation** for the entire app and surface spend at three levels — per turn, per
conversation, and consolidated per project (SPEC §3.6). Tokens are ground truth; cost is always
**computed from current catalog prices**, so a price change reprices historical usage.

## Deliverables
1. **`ICostService`** in `MeticulousResearch.Core`:
   - `ComputeTurnCost(usage, model)` — pure function: `input×priceIn + output×priceOut +
     cacheRead×priceCacheRead + cacheWrite×priceCacheWrite`, prices per MTok from the catalog.
   - `GetConversationCost(conversationId)` — running total (tokens + cost) over completed turns.
   - `GetProjectCost(projectId, window)` — consolidated total with breakdowns by
     **source** (conversations vs artifact generations), **by model**, and **by time window**
     (today / this week / all-time).
2. **Price lookup** — reads the config-driven model catalog (§6.3) owned by `model-selector`;
   prices in USD/MTok including `priceCacheReadMTok` / `priceCacheWriteMTok` where present.
3. **Repricing semantics** — totals are always recomputed from stored **token** columns
   (`Message.tokens_*`, `ArtifactVersion.tokens_*`), never from the snapshot `cost_usd`. The
   snapshot is retained for audit only.
4. **Unknown-price handling** — a model absent from the catalog yields an `unknown-price` flag;
   such usage is surfaced but excluded from priced totals (never counted as $0).
5. **View-model** for the consolidated cost panel on the project dashboard, plus per-turn badge
   and conversation-header running total (wired by their respective features).
6. **Optional soft budget** — a per-project monthly threshold that raises a non-blocking warning;
   off by default.

## Suggested design
- Keep `ComputeTurnCost` a **pure, side-effect-free** function so it is trivially `@unit`-testable
  with a fixed price table — this is the heart of the feature.
- Represent money in a decimal type with generous precision (fractional cents matter for
  per-turn badges); round only at the display layer.
- Inject `IClock` for time-window bucketing (today = clock's local date; week = rolling 7 days
  or ISO week — pick one and encode it in the tests). See TESTING-STRATEGY §4.
- Compute cost lazily on read from tokens; do not persist a second authoritative cost. The
  `cost_usd` snapshot column (§5) is written at turn completion for offline/audit display only.
- Breakdowns are aggregation queries over `Message` (source = conversation) and
  `ArtifactVersion` (source = artifact) joined to the price table in memory.

## Test-first order
1. `ComputeTurnCost` `@unit` tests (formula, cache rates, precision, unknown price) → pure function.
2. Conversation-total `@unit` tests → aggregation over turns.
3. Consolidated-by-source / by-model / by-window `@unit` tests (with injected clock) → project aggregation.
4. Repricing + snapshot-vs-current `@unit` tests → recompute-from-tokens semantics.
5. Provenance `@unit` tests → authoritative-usage marking; exclude estimates.
6. Budget guardrail `@unit` tests → soft warning.
7. `@ui` tests (per-turn badge + hover, conversation header live update, dashboard panel) → view wiring.

## Definition of done
- All `@unit` scenarios green against the fixed price table and injected clock.
- Totals recompute from tokens; a price update changes historical totals with no token mutation.
- Unknown-price usage is flagged and excluded, never silently zero.
- Consolidated panel shows all three breakdowns on the dashboard.

## Notes for later features
- `usage-csv-export` consumes `ICostService` per-turn rows — expose an enumerable of priced
  turn records (turn id, conversation/artifact, model, tokens, computed cost, timestamp).
- `backup-restore` moves the token columns as-is; cost need not be serialized since it recomputes.
- If a future price catalog adds per-model cache rates that differ from today's defaults, only
  the catalog changes — `ComputeTurnCost` already reads them by field.

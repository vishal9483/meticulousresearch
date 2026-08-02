# Phase — Prompt Caching

**SPEC:** §8, §3.6. **Milestone:** M2. **Depends on:** ai-gateway

## Goal
Send the **system prompt (custom instructions)** and **stable resource context** with **cache
breakpoints** so repeated turns and regenerations reuse cached input — cutting latency and cost —
and **meter cache-read/write tokens** so they flow into per-turn cost (§3.6). A v1 core
reliability/efficiency feature (§8).

## Deliverables
1. **Cache-breakpoint placement** — extend the grounding request assembly (from `conversations`,
   via `ai-gateway`) to mark stable segments as cacheable: the system prompt segment and the
   stable enabled-resource context segment; leave the volatile tail (recent history + new
   message) uncached.
2. **Cache-key stability** — the resource cache segment reflects the exact enabled scope; a scope
   change invalidates that segment (no stale reuse), while unchanged scope reuses it across turns
   and on retries/regenerations.
3. **Usage capture** — persist `tokens_cache_read` / `tokens_cache_write` on the `Message`
   (fields already in §5); default to 0 when the backend reports none.
4. **Cost integration** — per-turn cost includes cache-read and cache-write contributions at the
   catalog's cache rates; the badge breakdown itemizes them.

## Suggested design
- Both backends must express cache breakpoints: the Agent SDK sidecar via its prompt-caching
  helpers (primary, §7.2), the direct-API client via `cache_control` markers on content blocks.
  Keep the *placement decision* backend-agnostic in the assembler; each backend translates it.
- Order the prompt so the most stable content is first (system → stable resources → history →
  message) to maximize cache hits; breakpoints go at the end of stable segments.
- Cache-read/write tokens are authoritative from the backend usage fields (§3.6 provenance) —
  never estimated.
- Cost uses catalog cache rates by model id (from `model-selector`); if a catalog omits cache
  rates, treat cache tokens at 0 cost contribution but still record the token counts.
- Keep assembly + metering pure/`@unit`-testable with `FakeChatService` echoing breakpoints and
  reporting scripted cache usage.

## Test-first order
1. Breakpoint-placement `@unit` tests (system + stable resources cacheable; volatile tail not) →
   assembler extension.
2. Reuse/invalidation `@unit` tests (second turn reuses; scope change invalidates; retry reuses)
   → cache-key stability.
3. Metering `@unit` tests (cache_read/write recorded; missing → 0) → usage persistence.
4. Cost `@unit` + `@ui` tests (cache contributions in cost; breakdown itemizes) → cost integration.

## Definition of done
- System prompt and stable resource context carry cache breakpoints; the volatile tail does not.
- Unchanged instructions/scope reuse the cache across turns and regenerations (backend reports
  cache-read); changing scope invalidates the resource segment.
- Cache-read/write tokens are recorded (0 when absent) and included in per-turn cost using
  catalog cache rates; the breakdown itemizes them.
- Behavior is identical across sidecar and direct-API backends.

## Notes for later features
- `cost-tracking` (M4) rolls cache costs into per-chat and per-project consolidated totals.
- `rate-limit-backoff` retries benefit from cache reuse (cheaper/faster repeated input).
- `turn-metadata-actions` already itemizes cache read/write in the per-turn breakdown.

# Phase — Token Estimation

**SPEC:** §3.2, §3.6. **Milestone:** M1. **Depends on:** text-paste-resource

## Goal
Provide a **deterministic, local, network-free token estimator** used to populate each resource's
`token_estimate` and to drive the pre-send context estimate. Estimates are explicitly **labeled
"estimated"** — authoritative token counts come later from API usage fields (§3.6), not from here.

## Deliverables
1. **`ITokenEstimator`** in Core (consumed by all resource types since text-paste): a pure
   function `EstimateTokens(text) -> int`, deterministic and offline.
2. **Image-token estimate** — an `EstimateImageTokens(...)` (or equivalent) so image resources
   contribute toward context (coordinated with `image-vision-caption`).
3. **Resource wiring** — resources set `token_estimate` at add/re-extract time via this service.
4. **"Estimated" labeling** — a display convention so every surfaced estimate is marked estimated,
   distinct from authoritative usage counts.
5. **Documented tolerance** — a stated accuracy target vs. real model tokenization, asserted by a
   reference-text test.

## Suggested design
- Implement a heuristic approximation (e.g. character/word-based ratio) rather than shipping a
  full model tokenizer; keep it deterministic and dependency-light. Document the tolerance.
- Empty/whitespace → 0. Longer inputs → monotonically larger estimates.
- Keep the estimator pure (no I/O) so it's trivially `@unit`-testable and reusable by
  `context-budget` and pre-send checks.
- Expose image estimation separately since it's a different unit than text length.

## Test-first order
1. Determinism + no-network + empty=0 `@unit` tests → core estimator.
2. Monotonicity + tolerance-vs-reference `@unit` tests → tune the heuristic.
3. Per-resource + re-extract-recompute + image-contribution `@unit` tests → resource wiring.
4. "Estimated" labeling `@unit` / `@ui` tests → display convention.

## Definition of done
- Estimator is deterministic, offline, and within documented tolerance on reference text.
- Every resource's `token_estimate` is populated via this service and recomputed on re-extract.
- All estimates are labeled "estimated" wherever shown.

## Notes for later features
- `context-budget` sums enabled-resource estimates against the model context window + budget.
- `cost-tracking` (M4) uses **authoritative** API token counts for cost; this estimator is only
  for pre-send planning — keep the two clearly separated in the UI.
